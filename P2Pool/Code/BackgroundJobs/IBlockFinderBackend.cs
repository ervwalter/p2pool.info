using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace P2Pool
{
    interface IBlockFinderBackend
    {
        List<Block> GetNewBlocks(int lastKnownBlockHeight, string[] lastKnownBlockHashes, HashSet<string> knownTxHashes);
        List<Subsidy> GetNewSubsidies(int lastKnownBlockHeight, HashSet<string> p2poolAddresses);
    }
}
