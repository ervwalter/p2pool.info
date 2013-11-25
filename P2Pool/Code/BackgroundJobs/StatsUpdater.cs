using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using WebBackgrounder;
using Dapper;

namespace P2Pool
{
    public class StatsUpdater : Job
    {
        public StatsUpdater()
            : base("Stats Updater", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60))
        {
        }

        public override System.Threading.Tasks.Task Execute()
        {
            return new Task(() =>
            {
                UpdateStats();
            });
        }

        public event LogMessageDelegate LogMessage;

        private void Log(string message)
        {
            if (LogMessage != null)
            {
                LogMessage(message);
            }
        }

        public void UpdateStats()
        {
            try
            {
                int timestamp = DateTime.UtcNow.ToUnixTime();

                int remainder = timestamp % 300;
                timestamp -= remainder;

                Log(string.Format("Updating stats for timestamp {0}", timestamp));

                IEnumerable<string> servers = null;

                bool updatedData = false;
                P2PWebClient client = new P2PWebClient();
                client.RequestTimeout = 3000;

                Retry.ExecuteAction(() =>
                {
                    using (P2PoolDb db = new P2PoolDb())
                    {
                        var existingUsersCount = (from u in db.Users where u.Timestamp == timestamp select u.Address).Count();

                        if (existingUsersCount == 0)
                        {
                            JObject users = null;

                            if (servers == null)
                            {
                                servers = GetServers();
                            }
                            foreach (var server in servers)
                            {
                                if (string.IsNullOrWhiteSpace(server))
                                {
                                    continue;
                                }
                                try
                                {
                                    var baseUrl = new Uri(server.Trim());
                                    users = JObject.Parse(client.DownloadString(new Uri(baseUrl, "/users")));
                                    Log(string.Format(" Stats: Retrived users from {0}", server));
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    Log(" Stats: " + ex.Message);
                                }
                            }

                            if (users == null)
                            {
                                return;
                            }

                            Dictionary<string, decimal> addresses = new Dictionary<string, decimal>();
                            foreach (var userEntry in users.Properties())
                            {
                                string address = P2PHelper.ExtractAddress(userEntry.Name) ?? "Unknown";
                                if (address != null)
                                {
                                    decimal portion = 0;
                                    if (addresses.ContainsKey(address))
                                    {
                                        portion += addresses[address];
                                    }
                                    portion += (decimal)userEntry.Value;
                                    addresses[address] = portion;
                                }
                            }
                            foreach (var item in addresses)
                            {
                                User user = new User()
                                {
                                    Timestamp = timestamp,
                                    Address = item.Key,
                                    Portion = item.Value
                                };
                                db.Users.Add(user);

                            }
                            db.SaveChanges();
                            updatedData = true;
                            Log(string.Format(" Stats: Added {0} users", addresses.Count));
                        }


                    }
                });

                Retry.ExecuteAction(() =>
                {
                    using (P2PoolDb db = new P2PoolDb())
                    {


                        Stat entry = db.Stats.Find(timestamp);
                        if (entry == null)
                        {
                            decimal rate = -1;
                            if (servers == null)
                            {
                                servers = GetServers();
                            }
                            foreach (var server in servers)
                            {
                                if (string.IsNullOrWhiteSpace(server))
                                {
                                    continue;
                                }
                                try
                                {
                                    var baseUrl = new Uri(server.Trim());
                                    rate = decimal.Parse(client.DownloadString(new Uri(baseUrl, "/rate"))) / 1000000000m;
                                    Log(string.Format(" Stats: Retrived rate from {0}", server));
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    Log(" Stats: " + ex.Message);
                                }
                            }

                            if (rate == -1)
                            {
                                return;
                            }

                            int userCount;

                            userCount = db.Database.SqlQuery<int>("select count (distinct address) from p2pool_Users where timestamp >= @start and timestamp <= @end", new SqlParameter("start", timestamp - 86400), new SqlParameter("end", timestamp)).First();

                            entry = new Stat
                            {
                                Timestamp = timestamp,
                                Rate = rate,
                                Users = userCount
                            };
                            db.Stats.Add(entry);
                            db.SaveChanges();
                            updatedData = true;
                            Log(string.Format(" Stats: Saved new rate: {0}", rate));

                        }
                    }
                });

                if (updatedData)
                {

                    Retry.ExecuteAction(() =>
                    {
                        using (P2PoolDb db = new P2PoolDb())
                        {
                            CurrentPayouts entry = db.CurrentPayouts.Find(1);
                            if (entry != null)
                            {
                                JObject payouts = null;
                                if (servers == null)
                                {
                                    servers = GetServers();
                                }
                                foreach (var server in servers)
                                {
                                    if (string.IsNullOrWhiteSpace(server))
                                    {
                                        continue;
                                    }
                                    try
                                    {
                                        var baseUrl = new Uri(server.Trim());
                                        payouts = JObject.Parse(client.DownloadString(new Uri(baseUrl, "/current_payouts")));
                                        Log(string.Format(" Stats: Retrived payouts from {0}", server));
                                        break;
                                    }
                                    catch (Exception ex)
                                    {
                                        Log(" Stats: " + ex.Message);
                                    }
                                }
                                if (payouts == null)
                                {
                                    return;
                                }
                                entry.Payouts = payouts.ToString();
                                entry.Updated = timestamp;
                                db.SaveChanges();
                                Log(" Stats: Saved updated payouts");
                            }
                        }
                    });

                    //cleanup old user stats
                    Retry.ExecuteAction(() =>
                    {
                        using (P2PoolDb db = new P2PoolDb())
                        {
                            // delete all the specific user stats older than 3 days
                            var result = db.Database.ExecuteSqlCommand("delete from p2pool_Users where [Timestamp] < @cutoff", new SqlParameter("cutoff", timestamp - 259200));
                            Log(string.Format(" Stats: Deleted old user rows", result));
                        }
                    });

                }

            }
            catch (Exception ex)
            {
                Log(" Stats: UpdateStats Exception: " + ex.Message);
            }
        }

        private IEnumerable<string> GetServers()
        {
            using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["P2PoolDb"].ConnectionString))
            {
                connection.Open();
                return connection.Query<string>("SELECT Url FROM p2pool_Servers ORDER BY Priority DESC");
            }
        }
    }
}