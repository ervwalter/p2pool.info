using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebBackgrounder;

namespace P2Pool
{
    public class BlockFinder : Job
    {
        private List<IBlockFinderBackend> _backends = new List<IBlockFinderBackend>();

        public event LogMessageDelegate LogMessage;

        public BlockFinder() : base("Block Finder", TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(180))
        {
            //_backends.Add(new BitcoinDaemonBackend());
            _backends.Add(new BlockChainInfoBackend());
        }

        public override System.Threading.Tasks.Task Execute()
        {
            return new Task(() =>
            {
               UpdateDatabase();
            });
        }

        private void Log(string message)
        {
            if (LogMessage != null)
            {
                LogMessage(message);
            }
        }

        private List<Subsidy> GetNewSubsidies()
        {
            int lastKnownBlockHeight = 0;
//            HashSet<string> p2poolAddresses = null;
            Retry.ExecuteAction(() =>
            {
                using (P2PoolDb db = new P2PoolDb())
                {
                    lastKnownBlockHeight = (from b in db.Blocks
                                            orderby b.BlockHeight descending
                                            select b.BlockHeight).Take(1).FirstOrDefault();
                    //var p2poolAddressesQuery = (from u in db.Users
                    //                            select u.Address).Distinct();
                    //p2poolAddresses = new HashSet<string>(p2poolAddressesQuery);
                }
            });

            foreach (var backend in _backends)
            {
                try
                {
                    //return backend.GetNewSubsidies(lastKnownBlockHeight, p2poolAddresses);
                }
                catch
                {
                    //absorb
                }
            }

            return new List<Subsidy>();
        }


        private List<Block> GetNewBlocks()
        {
            int lastKnownBlockHeight = 0;
            string[] lastKnownBlockHashes = null;
            HashSet<string> knownTxHashes = null;
            Retry.ExecuteAction(() =>
            {
                using (P2PoolDb db = new P2PoolDb())
                {
                    lastKnownBlockHeight = (from b in db.Blocks
                                            orderby b.BlockHeight descending
                                            select b.BlockHeight).Take(1).FirstOrDefault();
                    lastKnownBlockHashes = (from b in db.Blocks
                                            where b.BlockHeight == lastKnownBlockHeight
                                            select b.Id).ToArray();
                    knownTxHashes = new HashSet<string>(from b in db.Blocks 
                                                        where b.IsP2Pool || b.IsFalseP2Pool
                                                        select b.GenerationTxHash);
                }
            });

            IEnumerable<Block> newBlocks = null;

            foreach (var backend in _backends)
            {
                try
                {
                    var someNewBlocks = backend.GetNewBlocks(lastKnownBlockHeight, lastKnownBlockHashes, knownTxHashes);
                    if (newBlocks == null)
                    {
                        newBlocks = someNewBlocks;
                    }
                    else
                    {
                        newBlocks = newBlocks.Union(someNewBlocks, new BlockEqualityComparer());
                    }
                    knownTxHashes.UnionWith(from b in someNewBlocks select b.GenerationTxHash);
                }
                catch
                {
                    //absorb
                }
            }

            return new List<Block>(newBlocks ?? new Block[] { });
        }

        public void UpdateDatabase()
        {
            try
            {
                //look for subsidies before we add new blocks because we depend on lastKnowBlockHeight to know where to start looking
                var subsidies = GetNewSubsidies();

                if (subsidies.Count > 0)
                {
                    Retry.ExecuteAction(() =>
                    {
                        using (P2PoolDb db = new P2PoolDb())
                        {
                            foreach (var subsidy in subsidies)
                            {
                                var existingSubsidy = db.Subsidies.Find(subsidy.TxHash);
                                if (existingSubsidy == null)
                                {
                                    Log(string.Format("Adding subsidy of {0} BTC, {1}", subsidy.Amount, subsidy.TxHash));
                                    db.Subsidies.Add(subsidy);
                                }
                                else if (existingSubsidy.BlockHeight != subsidy.BlockHeight || existingSubsidy.BlockHash != subsidy.BlockHash)
                                {
                                    Log(string.Format("Updating subsidy of {0} BTC, {1}", subsidy.Amount, subsidy.TxHash));
                                    existingSubsidy.BlockHash = subsidy.BlockHash;
                                    existingSubsidy.BlockHeight = subsidy.BlockHeight;
                                }
                                db.SaveChanges();
                            }
                        }
                    });
                }

                var blocks = GetNewBlocks();
                if (blocks.Count > 0)
                {
                    Retry.ExecuteAction(() =>
                    {
                        using (P2PoolDb db = new P2PoolDb())
                        {
                            foreach (var block in blocks)
                            {
                                var existingBlock = db.Blocks.Find(block.Id);
                                if (existingBlock == null)
                                {
                                    Log(string.Format("Adding block {0}, {1}{2}", block.BlockHeight, block.Id, block.IsP2Pool ? " (p2pool)" : ""));
                                    db.Blocks.Add(block);
                                }
                                else
                                {
                                    Log(string.Format("Updating block {0}, {1}{2}", block.BlockHeight, block.Id, block.IsP2Pool ? " (p2pool)" : ""));
                                    existingBlock.BlockHeight = block.BlockHeight;
                                    existingBlock.Difficulty = block.Difficulty;
                                    existingBlock.GenerationTxHash = block.GenerationTxHash;
                                    existingBlock.IsOrphaned = block.IsOrphaned;
                                    existingBlock.IsP2Pool = block.IsP2Pool;
                                    existingBlock.PrevBlock = block.PrevBlock;
                                    existingBlock.Timestamp = block.Timestamp;
                                }
                                db.SaveChanges();
                            }

                            //the following logic is too simplistic and only finds orphaned blocks if they are immediatly orphaned (vs a race between to candidate chains that lasts more than 1 block)

                            // this clears the orphaned flag from any blocks that now have a later block pointing at them
                            //db.Database.ExecuteSqlCommand("update p2pool_Blocks set isorphaned=0 where isorphaned=1 and exists (select * from p2pool_Blocks as b2 where b2.PrevBlock = p2pool_Blocks.Id)");

                            // this sets the orphaned flag for any blocks that aren't pointed at by some later block
                            db.Database.ExecuteSqlCommand("update p2pool_Blocks set isorphaned=1 where blockheight < (select max(blockheight) from p2pool_Blocks) and not exists (select * from p2pool_Blocks as b2 where b2.PrevBlock = p2pool_Blocks.Id)");
                        }
                    });
                }

            }
            catch (Exception ex)
            {
                Log("UpdateDatabase: " + ex.Message);
            }
        }
    }
}
