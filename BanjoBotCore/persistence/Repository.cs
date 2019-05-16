using BanjoBotCore.Model.DataTransfer;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BanjoBotCore.persistence
{
    //TODO: abstraction layer for persistenz (DAOs)
    //TODO: Implementation MySqlStorage (DatabaseController), MySqlLiteStorage, WebSocketStorage 
    //TODO: Abstract IRepository: connectionstring, url data
    public interface IRepository<T>
    {
        int Insert(T obj);
        int Update(T obj);
        int Delete(T obj);
        T FindAll();
    }

    public abstract class MySQLRepository<T> : IRepository<T>
    {
        public String ConnectionString { get; set; }

        public MySQLRepository(IServiceProvider serviceProvider)
        {
            //IConfiguration config = serviceProvider.GetService<IConfiguration>();
            //ConnectionString = config.GetValue<String>("DbConnectionString");
        }

        public abstract int Delete(T obj);
        public abstract T FindAll();
        public abstract int Insert(T obj);
        public abstract int Update(T obj);
    }

    public class LobbyRepository : MySQLRepository<LobbyDTO>
    {
        public override int Delete(LobbyDTO obj)
        {
            throw new NotImplementedException();
        }

        public override LobbyDTO FindAll()
        {
            throw new NotImplementedException();
        }

        public override int Insert(LobbyDTO obj)
        {
            throw new NotImplementedException();
        }

        public override int Update(LobbyDTO obj)
        {
            throw new NotImplementedException();
        }
    }
}
