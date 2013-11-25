using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Configuration;

namespace P2Pool.Controllers
{
    public class HomeController : Controller
    {
        private volatile static List<Block> _blocks = new List<Block>();
        private static DateTime _blocksExpire = DateTime.MinValue;

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Luck()
        {
            return View();
        }

		public string Messages(string key)
		{
			if (key != ConfigurationManager.AppSettings["AuthToken"])
			{
				return "";
			}

			Response.Headers.Add("Refresh", "5");

			return BackgrounderSetup.GetMessages();
		}
		
		[OutputCache(Duration = 300, Location = System.Web.UI.OutputCacheLocation.ServerAndClient)]
        [Compress]
        public ActionResult Stats(long? from)
        {
            Stopwatch timer = new Stopwatch();
            Debug.WriteLine("Starting stats update, from = {0}", from);
            timer.Start();
            return Retry.ExecuteAction<ActionResult>(() =>
            {
                using (P2PoolDb db = new P2PoolDb())
                {
                    int cutoff = 0;

                    var statsQuery = db.Stats.AsQueryable();

                    if (from.HasValue)
                    {
                        cutoff = (int)from.Value / 1000;
                        statsQuery = statsQuery.Where(s => s.Timestamp > cutoff);
                    }

                    var ratesQuery = (from s in statsQuery
                                      group s by Math.Floor(s.Timestamp / 7200m) into grouped
                                      select new { Timestamp = grouped.Key * 7200000, Rate = grouped.Average(x => x.Rate), Users = grouped.Max(x => x.Users) });

                    if (from.HasValue)
                    {
                        ratesQuery = ratesQuery.Where(x => x.Timestamp > from.Value);
                    }

                    var rates = ratesQuery.ToList();

                    if (rates.Count == 0)
                    {
                        return Json(new
                        {
                            rates = new int[] { },
                            users = new int[] { }
                        }, JsonRequestBehavior.AllowGet);
                    }

                    var ratesArray = (from r in rates
                                      orderby r.Timestamp
                                      select new List<object> { (long)r.Timestamp, Math.Round((double)r.Rate, 1) }).ToList();

                    var users = (from r in rates
                                 orderby r.Timestamp
                                 select new List<object> { (long)r.Timestamp, (int)r.Users }).ToList();


                    var maxRate = (from s in db.Stats
                                   select s.Rate).Max();
                    var maxUsers = (from s in db.Stats
                                    select s.Users).Max();


                    var result = new
                    {
                        rates = ratesArray,
                        users = users.ToList(),
                        maxRate = maxRate,
                        maxUsers = maxUsers,
                    };

                    string json = JsonConvert.SerializeObject(result);

                    timer.Stop();
                    Debug.WriteLine("..End stats update, from = {0}, time = {1}", from, timer.ElapsedMilliseconds / 1000m);

                    return Content(json, "application/json");
                }
            });
        }

        [OutputCache(Duration = 180, Location = System.Web.UI.OutputCacheLocation.Client)]
        [Compress]
        public ActionResult Payouts()
        {
            return Retry.ExecuteAction<ActionResult>(() =>
            {
                using (P2PoolDb db = new P2PoolDb())
                {
                    string payoutsJson = db.CurrentPayouts.Find(1).Payouts;
                    var payouts = JObject.Parse(payoutsJson);

                    var response = (from a in
                                        (from p in payouts.Properties()
                                         select new
                                         {
                                             Address = P2PHelper.ExtractAddress(p.Name) ?? "Unknown",
                                             Payment = (decimal)p.Value
                                         })
                                    group a by a.Address into g
                                    select new
                                    {
                                        Address = g.Key,
                                        Payment = g.Sum(a => a.Payment)
                                    })
                                    .OrderByDescending(p => p.Payment).ToList();

                    return Json(response, JsonRequestBehavior.AllowGet);
                }
            });
        }

        [OutputCache(Duration = 180, Location = System.Web.UI.OutputCacheLocation.Client)]
        [Compress]
        public ActionResult Donations()
        {
            return Retry.ExecuteAction<ActionResult>(() =>
            {
                using (P2PoolDb db = new P2PoolDb())
                {
                    var response = (from s in db.Subsidies
                                    orderby s.BlockHeight descending
                                    select s).ToList();
                    return Json(response, JsonRequestBehavior.AllowGet);
                }
            });
        }

        [OutputCache(Duration = 300, Location = System.Web.UI.OutputCacheLocation.ServerAndClient)]
        public decimal Difficulty()
        {
            P2PWebClient client = new P2PWebClient();
            client.RequestTimeout = 10000;
			string data = client.DownloadString("http://blockchain.info/q/getdifficulty");
            return decimal.Parse(data, System.Globalization.NumberStyles.Float);
        }

        [OutputCache(Duration = 180, Location = System.Web.UI.OutputCacheLocation.Client)]
        [Compress]
        public ActionResult Users() 
        {
            return Retry.ExecuteAction<ActionResult>(() =>
            {
                using (P2PoolDb db = new P2PoolDb())
                {
                    int cutoff = (int)(DateTime.UtcNow.AddHours(-24) - new DateTime(1970, 1, 1)).TotalSeconds;

                    var rawUsers = (from u in db.Users
                                    where u.Timestamp > cutoff
                                    select u).ToList();

                    decimal sum = rawUsers.Sum(u => u.Portion);

                    List<Stat> stats = (from s in db.Stats
                                        where s.Timestamp > (cutoff - 86400)
                                        select s).ToList();

                    decimal totalHashrate = GetAverageHashBetweenTimes(cutoff, cutoff + 86400, stats);

                    var response = (from u in rawUsers
                                    group u by u.Address into g
                                    select new
                                    {
                                        Address = g.Key,
                                        Hashrate = Math.Round(totalHashrate * (g.Sum(u => u.Portion) / sum) * 1000, 0)
                                    })
                                    .OrderByDescending(u => u.Hashrate).Select(u => new
                                    {
                                        Address = u.Address,
                                        Hashrate = u.Hashrate.ToString("#,0 MH/s")
                                    });


                    return Json(response, JsonRequestBehavior.AllowGet);
                }
            });
        }


        [OutputCache(Duration = 180, Location = System.Web.UI.OutputCacheLocation.Client)]
        [Compress]
        public ActionResult Blocks(int? from, bool? all)
        {
            List<Block> blocks = GetBlocks();

            if (from != null)
            {
                blocks = _blocks.Where(b => b.BlockHeight > from).OrderBy(b => b.BlockHeight).ThenBy(b => b.Timestamp).ToList();
            }
            else if (!all.GetValueOrDefault())
            {
                if (blocks.Count > 0)
                {
                    int cutoff = blocks[0].Timestamp - 7776000;
                    blocks = (from b in blocks where b.Timestamp >= cutoff select b).ToList();
                }
            }

            return Json(blocks, JsonRequestBehavior.AllowGet);
        }

        private List<Block> GetBlocks()
        {
            Stopwatch timer = new Stopwatch();
            Debug.WriteLine("Starting blocks update");
            timer.Start();
            return Retry.ExecuteAction<List<Block>>(() =>
            {
                using (P2PoolDb db = new P2PoolDb())
                {
                    var dbBlockCount = (from b in db.Blocks where b.IsP2Pool select b).Count();

                    if (DateTime.Now < _blocksExpire && _blocks != null && dbBlockCount == _blocks.Count)
                    {
                        var lastBlock = (from b in db.Blocks where b.IsP2Pool orderby b.BlockHeight descending select b).First();
                        if (lastBlock.Id == _blocks[0].Id && lastBlock.IsOrphaned == _blocks[0].IsOrphaned)
                        {
                            return _blocks;
                        }
                    }

                    var blocks = (from b in db.Blocks where b.IsP2Pool orderby b.BlockHeight, b.Timestamp select b).ToList();

                    try
                    {
                        //build hashrate list
                        List<Stat> stats = (from s in db.Stats orderby s.Timestamp select s).ToList();

                        Block lastBlock = null;
                        foreach (var block in blocks)
                        {
                            if (lastBlock != null)
                            {
                                block.RoundDuration = block.Timestamp - lastBlock.Timestamp;
                                decimal averageRateBetweenBlocks = GetAverageHashBetweenTimes(lastBlock.Timestamp, block.Timestamp, stats) * 1000000000;
                                decimal difficulty = block.Difficulty;
                                block.ExpectedDuration = (int)(difficulty * 4294967296 / averageRateBetweenBlocks);
                                block.ActualShares = (long)((block.RoundDuration * averageRateBetweenBlocks) / 4294967296);
                                block.ExpectedShares = (long)difficulty;


                            }
                            lastBlock = block;
                        }
                        blocks.Sort(new Comparison<Block>((a, b) =>
                        {
                            var result = b.BlockHeight.CompareTo(a.BlockHeight);
                            if (result == 0)
                            {
                                result = b.Timestamp.CompareTo(a.Timestamp);
                            }
                            return result;
                        }));
                        _blocks = blocks;
                        _blocksExpire = DateTime.Now.AddHours(2);
                        return blocks;
                    }
                    catch
                    {
                        return _blocks;
                    }
                    finally
                    {
                        timer.Stop();
                        Debug.WriteLine("..End blocks update, time = {0}", timer.ElapsedMilliseconds / 1000m);
                    }

                }
            });
        }

        private static decimal GetAverageHashBetweenTimes(int start, int end, List<Stat> stats)
        {
            Stat lastStat = null;
            decimal total = 0;
            long weight = 0;
            foreach (var stat in stats)
            {
                if (stat.Timestamp < start)
                {
                    lastStat = stat;
                    continue;
                }
                if (stat.Timestamp > end)
                {
                    if (weight == 0)
                    {
                        total = lastStat.Rate;
                        weight = 1;
                    }
                    break;
                }
                long timeSinceLastStat = stat.Timestamp - lastStat.Timestamp;
                total += stat.Rate * timeSinceLastStat;
                weight += timeSinceLastStat;
                lastStat = stat;
            }
            return total / weight;
        }


    }
}
