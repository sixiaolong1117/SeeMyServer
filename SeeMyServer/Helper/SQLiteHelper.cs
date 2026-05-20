using Microsoft.Data.Sqlite;
using SeeMyServer.Models;
using System;
using System.Collections.Generic;

namespace SeeMyServer.Datas
{
    public class SQLiteHelper
    {
        // 数据库版本号
        int DatabaseVersion = 1;

        // 连接到数据库文件
        private string connectionString = "Data Source=cms.db";

        public SQLiteHelper()
        {
            // 初始化数据库连接
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                // 打开连接
                connection.Open();
                // 如果不存在表则建表
                CreateTableIfNotExists(connection);
                // 每次初始化连接都对数据库进行一次升级检查
                UpgradeDatabase(connection);
            }
        }

        // 建表 如果不存在
        public void CreateTableIfNotExists(SqliteConnection connection)
        {
            // 创建信息表，存储服务器相关配置数据
            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = "CREATE TABLE IF NOT EXISTS CMSTable (Id INTEGER PRIMARY KEY, Name TEXT, HostIP TEXT, HostPort TEXT, SSHUser TEXT, SSHPasswd BLOB, SSHKey TEXT, OSType TEXT, SSHKeyIsOpen TEXT)";
            createTableCommand.ExecuteNonQuery();

            // 创建数据库版本表，用于指示当前数据库版本
            var createTableCommand2 = connection.CreateCommand();
            createTableCommand2.CommandText = "CREATE TABLE IF NOT EXISTS Version (VersionNumber INTEGER)";
            createTableCommand2.ExecuteNonQuery();
        }

        // 删表
        public void DropTable()
        {
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                // 连接打开
                connection.Open();

                // 删除CMSTable
                var dropTableCommand = connection.CreateCommand();
                dropTableCommand.CommandText = $"DROP TABLE IF EXISTS CMSTable;";
                dropTableCommand.ExecuteNonQuery();

                // 删除版本表
                var dropTableCommand2 = connection.CreateCommand();
                dropTableCommand2.CommandText = $"DROP TABLE IF EXISTS Version;";
                dropTableCommand2.ExecuteNonQuery();
            }
        }

        // 检查数据库版本
        public int GetDatabaseVersion(SqliteConnection connection)
        {
            // 查询数据库版本
            var selectVersion = connection.CreateCommand();
            selectVersion.CommandText = "SELECT VersionNumber FROM Version";
            // 获取结果
            var result = selectVersion.ExecuteScalar();
            // 如果有版本，返回结果
            if (result != null && int.TryParse(result.ToString(), out int version))
            {
                return version;
            }
            // 如果没有版本信息，返回-1
            return -1;
        }

        // 更新数据库版本信息
        public void UpgradeDatabaseVersion(SqliteConnection connection)
        {
            // 检查版本，新建数据库要插入版本号
            var cmd = connection.CreateCommand();
            // 数据库的版本号，由全局变量DatabaseVersion控制
            cmd.Parameters.AddWithValue("@VersionNumber", DatabaseVersion);
            // 没有版本号（插入版本号）
            if (GetDatabaseVersion(connection) == -1)
            {
                cmd.CommandText = "INSERT INTO Version (VersionNumber) VALUES (@VersionNumber)";
                cmd.ExecuteNonQuery();
            }
            // 存在版本号（更新版本号）
            else
            {
                cmd.CommandText = "UPDATE Version SET VersionNumber = @VersionNumber";
                cmd.ExecuteNonQuery();

            }
        }

        // 数据库升级
        // 如果数据库升级时不可避免的出现版本兼容问题，请在这里添加升级代码，确保旧数据库可以稳定转移到新数据库。
        public void UpgradeDatabase(SqliteConnection connection)
        {
            // 如果连接未打开，则打开
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();
            int currentVersion = GetDatabaseVersion(connection);

            // 检查当前数据库版本，如果小于软件数据库版本，则执行升级操作
            if (currentVersion < DatabaseVersion)
            {
                // 执行升级操作，例如添加新字段
                using (var cmd = connection.CreateCommand())
                {
                    // 具体操作
                }

                // 更新数据库版本信息
                UpgradeDatabaseVersion(connection);
            }
        }

        // 插入数据
        // 输入格式CMSModel，在Model中有定义
        public int InsertData(CMSModel cmsModel)
        {
            int insertedID = 0;

            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                // 连接打开
                connection.Open();

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = "INSERT INTO CMSTable (Name, HostIP, HostPort, SSHUser, SSHPasswd, SSHKey, OSType, SSHKeyIsOpen) " +
                                            "VALUES (@Name, @HostIP, @HostPort, @SSHUser, @SSHPasswd, @SSHKey, @OSType, @SSHKeyIsOpen);" +
                                            "SELECT last_insert_rowid();";

                insertCommand.Parameters.AddWithValue("@Name", cmsModel.Name ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@HostIP", cmsModel.HostIP ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@HostPort", cmsModel.HostPort ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@SSHUser", cmsModel.SSHUser ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@SSHPasswd", cmsModel.SSHPasswd ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@SSHKey", cmsModel.SSHKey ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@OSType", cmsModel.OSType ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@SSHKeyIsOpen", cmsModel.SSHKeyIsOpen ?? (object)DBNull.Value);

                // 执行插入命令并获取插入行的ID
                insertedID = Convert.ToInt32(insertCommand.ExecuteScalar());
            }

            return insertedID;
        }

        // 删除数据
        public void DeleteData(CMSModel cmsModel)
        {
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = "DELETE FROM CMSTable WHERE Id = @Id";
                deleteCommand.Parameters.AddWithValue("@Id", cmsModel.Id);

                deleteCommand.ExecuteNonQuery();
            }
        }

        // 更新数据
        public void UpdateData(CMSModel cmsModel)
        {
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                // 连接打开
                connection.Open();

                var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE CMSTable SET Name = @Name, HostIP = @HostIP, HostPort = @HostPort, SSHUser = @SSHUser, SSHPasswd = @SSHPasswd, SSHKey = @SSHKey, OSType = @OSType, SSHKeyIsOpen = @SSHKeyIsOpen WHERE Id = @Id";

                updateCommand.Parameters.AddWithValue("@Id", cmsModel.Id);
                updateCommand.Parameters.AddWithValue("@Name", cmsModel.Name ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@HostIP", cmsModel.HostIP ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@HostPort", cmsModel.HostPort ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@SSHUser", cmsModel.SSHUser ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@SSHPasswd", cmsModel.SSHPasswd ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@SSHKey", cmsModel.SSHKey ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@OSType", cmsModel.OSType ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@SSHKeyIsOpen", cmsModel.SSHKeyIsOpen ?? (object)DBNull.Value);

                updateCommand.ExecuteNonQuery();
            }
        }

        // 查询数据
        public List<CMSModel> QueryData()
        {
            List<CMSModel> entries = new List<CMSModel>();

            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var queryCommand = connection.CreateCommand();
                queryCommand.CommandText = "SELECT * FROM CMSTable";

                using (SqliteDataReader reader = queryCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        CMSModel entry = new CMSModel
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            HostIP = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            HostPort = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            SSHUser = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            SSHPasswd = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            SSHKey = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            OSType = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            SSHKeyIsOpen = reader.IsDBNull(8) ? "" : reader.GetString(8)
                        };
                        entries.Add(entry);
                    }
                }
            }

            return entries;
        }
        public CMSModel GetDataById(int id)
        {
            CMSModel entry = null;

            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var queryCommand = connection.CreateCommand();
                queryCommand.CommandText = "SELECT * FROM CMSTable WHERE Id = @Id";
                queryCommand.Parameters.AddWithValue("@Id", id);

                using (SqliteDataReader reader = queryCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        entry = new CMSModel
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            HostIP = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            HostPort = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            SSHUser = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            SSHPasswd = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            SSHKey = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            OSType = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            SSHKeyIsOpen = reader.IsDBNull(8) ? "" : reader.GetString(8)
                        };
                    }
                }
            }

            return entry;
        }
    }
}
