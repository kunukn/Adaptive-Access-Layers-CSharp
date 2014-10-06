using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Transactions;

namespace Adaptive.Database.Dapper.TEST
{
    [TestClass]
    public class DapperDbTests
    {
        [TestInitialize]
        public void SetCulture()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void DapperDbConnectionTest()
        {
            SqlCeConnectionStringBuilder builder = new SqlCeConnectionStringBuilder()
                {
                    DataSource = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "DapperTests.sdf")
                };

            using (var con = new SqlCeConnection(builder.ToString()))
            {
                IDapperTestsDb dal = DapperAccessLayer.Create<IDapperTestsDb>(con);

                dal.ClearAccounts();

                // No rows
                Account[] accounts = dal.GetAllAccounts().ToArray();
                Assert.AreEqual(0, accounts.Length);

                // No single row
                Account account = dal.GetAccount(-1).SingleOrDefault();
                Assert.IsNull(account);

                // Insert row and select it back
                int affectedRows = dal.InsertAccount("DKK", "My DKK account");
                Assert.AreEqual(1, affectedRows);
                Assert.AreNotEqual(0, dal.GetAccountsByCurrency("DKK").Count());
                Assert.AreEqual(0, dal.GetAccountsByCurrency("XXX").Count());
                Assert.AreEqual("My DKK account", dal.GetAccountsByCurrency("DKK").Single().Description);

                // Insert one more row
                dal.InsertAccount(new Account() { AccountCurrency = "USD", Description = "USD" });
                Assert.AreEqual("USD", dal.GetAccountsByCurrency("USD").Single().Description);

                // Insert many accounts
                dal.ClearAccounts();
                accounts = Enumerable.Range(0, 100).Select(i => new Account() { Description = i.ToString(), AccountCurrency = "GBP" }).ToArray();
                Assert.AreEqual(accounts.Length, dal.InsertAccounts(accounts));
                Assert.AreEqual(accounts.Length, dal.CountAccounts().Single());

                // Clean up
                Assert.AreNotEqual(0, dal.ClearAccounts());
            }

            // Test transaction
            using (var con = new SqlCeConnection(builder.ToString()))
            {
                IDapperTestsDb dal = DapperAccessLayer.Create<IDapperTestsDb>(con);
                using (var tran = new TransactionScope())
                {
                    con.Open();

                    // Insert accounts
                    dal.InsertAccount(new Account() { AccountCurrency = "USD", Description = "USD" });
                    Assert.AreEqual(1, dal.CountAccounts().Single());

                    // Don't commit
                }
                Assert.AreEqual(0, dal.CountAccounts().Single());
            }
        }

        [TestMethod]
        public void DapperDbMultiInstanceTest()
        {
            SqlCeConnectionStringBuilder builder = new SqlCeConnectionStringBuilder()
                {
                    DataSource = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "DapperTests.sdf")
                };

            for (int i = 0; i < 100; i++)
                DapperAccessLayer.Create<IDapperTestsDb>(() => new SqlCeConnection(builder.ToString()));
        }

        public interface IDapperTestsDb
        {
            [Query("SELECT * FROM [dbo.Accounts]")]
            IEnumerable<Account> GetAllAccounts();

            [Query("SELECT * FROM [dbo.Accounts] where @accountid=AccountId")]
            IEnumerable<Account> GetAccount(int accountId);

            [Query("SELECT * FROM [dbo.Accounts] where @currency=AccountCurrency")]
            IEnumerable<Account> GetAccountsByCurrency(string currency);

            [NonQuery("INSERT INTO [dbo.Accounts] (accountCurrency, Description) VALUES (@currency, @desc)")]
            int InsertAccount(string currency, string desc);

            [NonQuery("INSERT INTO [dbo.Accounts] (accountCurrency, Description) VALUES (@AccountCurrency, @Description)")]
            void InsertAccount(Account account);

            [NonQuery("INSERT INTO [dbo.Accounts] (accountCurrency, Description) VALUES (@AccountCurrency, @Description)")]
            int InsertAccounts(IEnumerable<Account> accounts);

            [NonQuery("DELETE FROM [dbo.Accounts]")]
            int ClearAccounts();

            [Query("SELECT Count(*) FROM [dbo.Accounts]")]
            IEnumerable<int> CountAccounts();
        }

        public class Account
        {
            public int AccountId { get; set; }
            public string AccountCurrency { get; set; }
            public string Description { get; set; }
        }
    }
}
