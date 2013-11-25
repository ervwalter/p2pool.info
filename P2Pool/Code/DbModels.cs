using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.ComponentModel.DataAnnotations;
using System.Configuration;

namespace P2Pool
{
	public class Stat
	{
		public int Timestamp { get; set; }
		public decimal Rate { get; set; }
		public int Users { get; set; }
	}

	public class Block
	{
		public string Id { get; set; }
		public int BlockHeight { get; set; }
		public int Timestamp { get; set; }
		public decimal Difficulty { get; set; }
		public string GenerationTxHash { get; set; }
		public string PrevBlock { get; set; }
		public bool IsP2Pool { get; set; }
		public bool IsFalseP2Pool { get; set; }
		public bool IsOrphaned { get; set; }

		[NotMapped]
		public int RoundDuration { get; set; }
		[NotMapped]
		public int ExpectedDuration { get; set; }
		[NotMapped]
		public long ActualShares { get; set; }
		[NotMapped]
		public long ExpectedShares { get; set; }

	}

	public class Subsidy
	{
		public string TxHash { get; set; }
		public int Timestamp { get; set; }
		public decimal Amount { get; set; }
		public int BlockHeight { get; set; }
		public string BlockHash { get; set; }
	}


	public class User
	{
		public string Address { get; set; }
		public int Timestamp { get; set; }
		public decimal Portion { get; set; }
	}

	public class CurrentPayouts
	{
		public int Id { get; set; }
		public string Payouts { get; set; }
		public int Updated { get; set; }
	}

	public class P2PoolDb : DbContext
	{
		public P2PoolDb()
			: base(P2PoolDb.ConnectionString())
		{
		}

		public static string ConnectionString()
		{
			return ConfigurationManager.ConnectionStrings["P2PoolDb"].ConnectionString;
		}

		public DbSet<Stat> Stats { get; set; }
		public DbSet<Block> Blocks { get; set; }
		public DbSet<User> Users { get; set; }
		public DbSet<Subsidy> Subsidies { get; set; }
		public DbSet<CurrentPayouts> CurrentPayouts { get; set; }

		protected override void OnModelCreating(DbModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Stat>().ToTable("p2pool_Stats");
			modelBuilder.Entity<Stat>().HasKey(s => s.Timestamp);
			modelBuilder.Entity<Stat>().Property(s => s.Timestamp).HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);

			modelBuilder.Entity<Subsidy>().ToTable("p2pool_Subsidies");
			modelBuilder.Entity<Subsidy>().HasKey(s => s.TxHash);

			modelBuilder.Entity<CurrentPayouts>().ToTable("p2pool_CurrentPayouts");
			modelBuilder.Entity<CurrentPayouts>().Property(c => c.Updated).HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
			modelBuilder.Entity<CurrentPayouts>().Property(c => c.Payouts).IsMaxLength();

			modelBuilder.Entity<User>().ToTable("p2pool_Users");
			modelBuilder.Entity<User>().HasKey(u => new { u.Address, u.Timestamp });
			modelBuilder.Entity<User>().Property(u => u.Timestamp).HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
			modelBuilder.Entity<User>().Property(u => u.Portion).HasPrecision(18, 8);
			modelBuilder.Entity<User>().Property(u => u.Address).IsMaxLength();

			modelBuilder.Entity<Block>().ToTable("p2pool_Blocks");
		}
	}

	public class BlockEqualityComparer : EqualityComparer<Block>
	{
		public override bool Equals(Block x, Block y)
		{
			return x.Id.Equals(y.Id);
		}

		public override int GetHashCode(Block obj)
		{
			return obj.Id.GetHashCode();
		}
	}

}