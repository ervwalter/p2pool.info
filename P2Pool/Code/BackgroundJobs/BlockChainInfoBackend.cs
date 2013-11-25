using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;
using HtmlAgilityPack;
using System.Diagnostics;

namespace P2Pool
{
	public class BlockChainInfoBackend : IBlockFinderBackend
	{
		private const string DonationAddress = "1Kz5QaUPDtKrj5SqW5tFkn7WZh8LmQaQi4";

		public List<Block> GetNewBlocks(int lastKnownBlockHeight, string[] lastKnownBlockHashes, HashSet<string> knownTxHashes)
		{
			List<Block> newBlocks = new List<Block>();

			int currentBlockHeight = GetCurrentBlockHeight();

			for (var height = lastKnownBlockHeight - 2; height <= currentBlockHeight; height++)
			{
				Debug.WriteLine("Getting Height: {0}", height);
				newBlocks.AddRange(GetBlocksAtHeight(height));
			}

            knownTxHashes.UnionWith(newBlocks.Select(b => b.Id));

            newBlocks.AddRange(FindOrphans(knownTxHashes));

			return newBlocks;
		}

		private List<Block> GetBlocksAtHeight(int height)
		{
			List<Block> blocks = new List<Block>();
			P2PWebClient client = new P2PWebClient();
			client.RequestTimeout = 10000;
			var blocksAtHeight = JObject.Parse(client.DownloadString(string.Format("http://blockchain.info/block-height/{0}?format=json", height)));

			foreach (var blockData in (JArray)blocksAtHeight["blocks"])
			{
				Block block = new Block();
				block.Id = (string)blockData["hash"];
				block.BlockHeight = (int)blockData["height"];
				block.Difficulty = (decimal)BitcoinMathHelper.Difficulty((long)blockData["bits"]);
				block.PrevBlock = (string)blockData["prev_block"];
				block.Timestamp = (int)blockData["time"];
				block.IsOrphaned = !(bool)blockData["main_chain"];
				var genTx = blockData["tx"][0];
				block.GenerationTxHash = (string)genTx["hash"];
				var outputs = (JArray)genTx["out"];
				foreach (var output in outputs)
				{
					if ((string)output["addr"] == DonationAddress)
					{
						var lastOutput = outputs[outputs.Count - 1];
						if (outputs.Count > 25 && (decimal)lastOutput["value"] == 0 && (decimal)lastOutput["type"] == -1)
						{
							block.IsP2Pool = true;
						}
						break;
					}
				}
				blocks.Add(block);
			}
			return blocks;
		}

		private int GetCurrentBlockHeight()
		{
			P2PWebClient client = new P2PWebClient();
			client.RequestTimeout = 10000;
			string data = client.DownloadString("http://blockchain.info/q/getblockcount");
			return int.Parse(data);
		}

        public List<Block> FindOrphans(HashSet<string> knownTxHashes)
        {
            List<Block> newBlocks = new List<Block>();
            P2PWebClient client = new P2PWebClient();
            client.RequestTimeout = 30000;
            var addressData = JObject.Parse(client.DownloadString("http://blockchain.info/address/1Kz5QaUPDtKrj5SqW5tFkn7WZh8LmQaQi4?format=json&filter=0"));

            foreach (var tx in (JArray)addressData["txs"])
            {
                try
                {
                    if (tx["inputs"] != null && tx["inputs"].Type == JTokenType.Array && ((JArray)tx["inputs"]).Count == 1 && ((JArray)tx["inputs"])[0]["prev_out"] == null)
                    {
                        string generationTxHash = (string)tx["hash"];
                        if (!knownTxHashes.Contains(generationTxHash))
                        {
                            int blockHeight;
                            if (tx["block_height"] != null)
                            {
                                blockHeight = (int)tx["block_height"];
                                var blocksAtHeight = JObject.Parse(client.DownloadString(string.Format("http://blockchain.info/block-height/{0}?format=json", blockHeight)));

                                foreach (var blockData in (JArray)blocksAtHeight["blocks"])
                                {
                                    try
                                    {
                                        if (blockData["tx"] != null && blockData["tx"][0] != null && blockData["tx"][0]["hash"] != null && (string)blockData["tx"][0]["hash"] == generationTxHash
                                            && blockData["tx"][0]["out"] is JArray && ((JArray)blockData["tx"][0]["out"]).Count > 25)
                                        {
                                            Block block = new Block();
                                            block.Id = (string)blockData["hash"];
                                            block.BlockHeight = (int)blockData["height"];
                                            block.Difficulty = (decimal)BitcoinMathHelper.Difficulty((long)blockData["bits"]);
                                            block.GenerationTxHash = generationTxHash;
                                            block.IsP2Pool = true;
                                            block.PrevBlock = (string)blockData["prev_block"];
                                            block.Timestamp = (int)blockData["time"];
                                            block.IsOrphaned = !(bool)blockData["main_chain"];
                                            newBlocks.Add(block);
                                            break;
                                        }
                                    }
                                    catch
                                    {
                                        //absorb and check next block
                                    }
                                }
                            }
                            else
                            {
                                string TxDataHtml = client.DownloadString(string.Format("http://blockchain.info/tx/{0}", generationTxHash));
                                bool doneParsing = false;

                                HtmlDocument document = new HtmlDocument();
                                document.LoadHtml(TxDataHtml);
                                var tables = document.DocumentNode.SelectNodes("//table");
                                foreach (var table in tables)
                                {
                                    var header = table.SelectSingleNode("tr/th");
                                    if (header == null || header.InnerText.ToLower() != "summary")
                                    {
                                        continue;
                                    }
                                    var links = table.SelectNodes(".//a");
                                    foreach (var link in links)
                                    {
                                        var blockUrl = link.GetAttributeValue("href", "");
                                        if (blockUrl.StartsWith("/block-index/"))
                                        {
                                            var blockData = JObject.Parse(client.DownloadString(string.Format("http://blockchain.info{0}?format=json", blockUrl)));

                                            try
                                            {
                                                if (((JArray)blockData["tx"][0]["out"]).Count > 10)
                                                {

                                                    Block block = new Block();
                                                    block.Id = (string)blockData["hash"];
                                                    block.BlockHeight = (int)blockData["height"];
                                                    block.Difficulty = (decimal)BitcoinMathHelper.Difficulty((long)blockData["bits"]);
                                                    block.GenerationTxHash = generationTxHash;
                                                    block.IsP2Pool = true;
                                                    block.PrevBlock = (string)blockData["prev_block"];
                                                    block.Timestamp = (int)blockData["time"];
                                                    block.IsOrphaned = !(bool)blockData["main_chain"];
                                                    newBlocks.Add(block);
                                                }

                                            }
                                            catch
                                            {
                                                //absorb
                                            }
                                            doneParsing = true;
                                            break;
                                        }
                                    }
                                    if (doneParsing)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    //absorb and check next tx
                }
            }

            return newBlocks;
        }

		public List<Subsidy> GetNewSubsidies(int lastKnownBlockHeight, HashSet<string> p2poolAddresses)
		{
			return new List<Subsidy>();
		}
	}
}