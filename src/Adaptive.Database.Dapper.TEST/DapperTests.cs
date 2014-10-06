using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

namespace Adaptive.Database.Dapper.TEST
{
    [TestClass]
    public class DapperTests
    {
        [TestInitialize]
        public void SetCulture()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void SimpleDapperLayerTest()
        {
            DbConnectionMock con = new DbConnectionMock();

            IDAL dal = DapperAccessLayer.Create<IDAL>(con);
            Assert.IsNotNull(dal);
            Assert.AreSame(dal.GetType(), DapperAccessLayer.Implement(typeof(IDAL)));
            DapperAccessLayer.Create<IDAL>(con);

            con.Table = new DataTable();
            con.Table.Columns.Add("A");
            con.Table.LoadDataRow(new[] { "a1" }, true);

            DTO[] rows = dal.GetDTOs(new DTO() { A = "a", B = "b" }).ToArray();
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("a1", rows[0].A);
            Assert.AreEqual(2, con.LastCommand.Parameters.Count);
            Assert.AreEqual("a", ((IDbDataParameter)con.LastCommand.Parameters[0]).Value);
            Assert.AreEqual("b", ((IDbDataParameter)con.LastCommand.Parameters[1]).Value);

            rows = dal.GetDTOs("42", 42).ToArray();
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("a1", rows[0].A);
            Assert.AreEqual(2, con.LastCommand.Parameters.Count);
            Assert.AreEqual("42", ((IDbDataParameter)con.LastCommand.Parameters[0]).Value);
            Assert.AreEqual(42, ((IDbDataParameter)con.LastCommand.Parameters[1]).Value);

            rows = dal.GetDTOsDynamic(new { A = "a", B = "b" }).ToArray();
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("a1", rows[0].A);
            Assert.AreEqual(2, con.LastCommand.Parameters.Count);
            Assert.AreEqual("a", ((IDbDataParameter)con.LastCommand.Parameters[0]).Value);
            Assert.AreEqual("b", ((IDbDataParameter)con.LastCommand.Parameters[1]).Value);
        }
    }

    public interface IDAL
    {
        [Query("SELECT * FROM DTOS where A=@a AND B=@b")]
        IEnumerable<DTO> GetDTOs(DTO input);

        [Query("SomeProc", CommandType.StoredProcedure, Buffered=false)]
        IEnumerable<DTO> GetDTOs(string A, int B);

        [Query("SomeProc", CommandType.StoredProcedure, Buffered = false)]
        IEnumerable<DTO> GetDTOsDynamic(dynamic p);
    }

    public class DTO
    {
        public string A { get; set; }
        public string B { get; set; }
    }

    internal class DbConnectionMock : IDbConnection
    {
        public DataTable Table { get; set; }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            throw new NotImplementedException();
        }

        public IDbTransaction BeginTransaction()
        {
            throw new NotImplementedException();
        }

        public void ChangeDatabase(string databaseName)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            State = ConnectionState.Closed;
        }

        public string ConnectionString { get; set; }

        public int ConnectionTimeout
        {
            get { throw new NotImplementedException(); }
        }

        public DbCommandMock LastCommand;

        public IDbCommand CreateCommand()
        {
            return LastCommand = new DbCommandMock() { Connection = this };
        }

        public string Database
        {
            get { throw new NotImplementedException(); }
        }

        public void Open()
        {
            State = ConnectionState.Open;
        }

        public ConnectionState State { get; set; }

        public void Dispose()
        {
        }
    }

    internal class DbCommandMock : IDbCommand
    {
        public void Cancel()
        {
        }

        public string CommandText { get; set; }
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDbConnection Connection { get; set; }
        public IDbDataParameter CreateParameter()
        {
            return new ParameterMock();
        }

        public int ExecuteNonQuery()
        {
            throw new NotImplementedException();
        }

        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            return ((DbConnectionMock)Connection).Table.CreateDataReader();
        }

        public IDataReader ExecuteReader()
        {
            throw new NotImplementedException();
        }

        public object ExecuteScalar()
        {
            throw new NotImplementedException();
        }

        private readonly ParameterColletionMock _parameters = new ParameterColletionMock();

        public IDataParameterCollection Parameters
        {
            get { return _parameters; }
        }

        public void Prepare()
        {
            throw new NotImplementedException();
        }

        public IDbTransaction Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Dispose()
        {
        }
    }

    internal class ParameterMock : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable { get; set; }
        public string ParameterName { get; set; }
        public string SourceColumn { get; set; }
        public DataRowVersion SourceVersion { get; set; }
        public object Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    internal class ParameterColletionMock : List<IDataParameter>, IDataParameterCollection
    {
        public bool Contains(string parameterName)
        {
            throw new NotImplementedException();
        }

        public int IndexOf(string parameterName)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(string parameterName)
        {
            throw new NotImplementedException();
        }

        public object this[string parameterName]
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
    }

}
