using Microsoft.Data.Sqlite;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SeeMyServer.Datas
{
    public class SQLiteHelper
    {
        // 数据库版本号
        int DatabaseVersion = 2;

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
            createTableCommand.CommandText = "CREATE TABLE IF NOT EXISTS CMSTable (Id INTEGER PRIMARY KEY, Name TEXT, HostIP TEXT, HostPort TEXT, SSHUser TEXT, SSHPasswd BLOB, SSHKey TEXT, SSHKeyId TEXT, OSType TEXT, SSHKeyIsOpen TEXT)";
            createTableCommand.ExecuteNonQuery();

            // 确保SSHKeyId列存在（兼容旧数据库升级）
            EnsureColumn(connection, "CMSTable", "SSHKeyId", "TEXT");

            // 创建SSH密钥管理表
            CreateSSHKeyTable(connection);

            // 创建数据库版本表，用于指示当前数据库版本
            var createTableCommand2 = connection.CreateCommand();
            createTableCommand2.CommandText = "CREATE TABLE IF NOT EXISTS Version (VersionNumber INTEGER)";
            createTableCommand2.ExecuteNonQuery();
        }

        // 创建SSH密钥表
        private static void CreateSSHKeyTable(SqliteConnection connection)
        {
            var createSSHKeyTableCommand = connection.CreateCommand();
            createSSHKeyTableCommand.CommandText = "CREATE TABLE IF NOT EXISTS SSHKeyTable (Id INTEGER PRIMARY KEY, Name TEXT, PrivateKey TEXT, PublicKey TEXT, Fingerprint TEXT, CreatedAt TEXT)";
            createSSHKeyTableCommand.ExecuteNonQuery();
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

                // 删除SSHKeyTable
                var dropSSHKeyTableCommand = connection.CreateCommand();
                dropSSHKeyTableCommand.CommandText = $"DROP TABLE IF EXISTS SSHKeyTable;";
                dropSSHKeyTableCommand.ExecuteNonQuery();

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
                // 升级到版本2：添加SSHKey表和相关字段
                if (currentVersion < 2)
                {
                    // 确保SSHKeyTable存在
                    CreateSSHKeyTable(connection);

                    // 确保CMSTable有SSHKeyId列
                    EnsureColumn(connection, "CMSTable", "SSHKeyId", "TEXT");

                    // 迁移现有SSHKey文件路径到SSHKeyTable
                    MigrateSSHKeyPaths(connection);

                    // 回填SSHKey元数据（公钥、指纹）
                    BackfillSSHKeyMetadata(connection);
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
                insertCommand.CommandText = "INSERT INTO CMSTable (Name, HostIP, HostPort, SSHUser, SSHPasswd, SSHKey, SSHKeyId, OSType, SSHKeyIsOpen) " +
                                            "VALUES (@Name, @HostIP, @HostPort, @SSHUser, @SSHPasswd, @SSHKey, @SSHKeyId, @OSType, @SSHKeyIsOpen);" +
                                            "SELECT last_insert_rowid();";

                insertCommand.Parameters.AddWithValue("@Name", cmsModel.Name ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@HostIP", cmsModel.HostIP ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@HostPort", cmsModel.HostPort ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@SSHUser", cmsModel.SSHUser ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@SSHPasswd", cmsModel.SSHPasswd ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@SSHKey", cmsModel.SSHKey ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@SSHKeyId", cmsModel.SSHKeyId ?? (object)DBNull.Value);
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
                updateCommand.CommandText = "UPDATE CMSTable SET Name = @Name, HostIP = @HostIP, HostPort = @HostPort, SSHUser = @SSHUser, SSHPasswd = @SSHPasswd, SSHKey = @SSHKey, SSHKeyId = @SSHKeyId, OSType = @OSType, SSHKeyIsOpen = @SSHKeyIsOpen WHERE Id = @Id";

                updateCommand.Parameters.AddWithValue("@Id", cmsModel.Id);
                updateCommand.Parameters.AddWithValue("@Name", cmsModel.Name ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@HostIP", cmsModel.HostIP ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@HostPort", cmsModel.HostPort ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@SSHUser", cmsModel.SSHUser ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@SSHPasswd", cmsModel.SSHPasswd ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@SSHKey", cmsModel.SSHKey ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@SSHKeyId", cmsModel.SSHKeyId ?? (object)DBNull.Value);
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
                        CMSModel entry = ReadCMSModel(reader);
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
                        entry = ReadCMSModel(reader);
                    }
                }
            }

            return entry;
        }

        private static CMSModel ReadCMSModel(SqliteDataReader reader)
        {
            return new CMSModel
            {
                Id = GetInt32OrDefault(reader, "Id"),
                Name = GetStringOrEmpty(reader, "Name"),
                HostIP = GetStringOrEmpty(reader, "HostIP"),
                HostPort = GetStringOrEmpty(reader, "HostPort"),
                SSHUser = GetStringOrEmpty(reader, "SSHUser"),
                SSHPasswd = GetStringOrEmpty(reader, "SSHPasswd"),
                SSHKey = GetStringOrEmpty(reader, "SSHKey"),
                SSHKeyId = GetStringOrEmpty(reader, "SSHKeyId"),
                OSType = GetStringOrEmpty(reader, "OSType"),
                SSHKeyIsOpen = GetStringOrEmpty(reader, "SSHKeyIsOpen")
            };
        }

        private static int GetInt32OrDefault(SqliteDataReader reader, string columnName)
        {
            int ordinal = GetOrdinalOrDefault(reader, columnName);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
            {
                return 0;
            }

            return Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static string GetStringOrEmpty(SqliteDataReader reader, string columnName)
        {
            int ordinal = GetOrdinalOrDefault(reader, columnName);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
            {
                return "";
            }

            object value = reader.GetValue(ordinal);
            return value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                _ => Convert.ToString(value) ?? ""
            };
        }

        private static int GetOrdinalOrDefault(SqliteDataReader reader, string columnName)
        {
            try
            {
                return reader.GetOrdinal(columnName);
            }
            catch (IndexOutOfRangeException)
            {
                return -1;
            }
        }

        #region SSH Key Management

        // 插入SSH密钥
        public int InsertSSHKey(SSHKeyModel model)
        {
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                return InsertSSHKey(connection, model);
            }
        }

        private static int InsertSSHKey(SqliteConnection connection, SSHKeyModel model)
        {
            SSHKeyModel enrichedModel = EnsureSSHKeyMetadata(model);
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO SSHKeyTable (Name, PrivateKey, PublicKey, Fingerprint, CreatedAt) VALUES (@Name, @PrivateKey, @PublicKey, @Fingerprint, @CreatedAt)";
            insertCommand.Parameters.AddWithValue("@Name", enrichedModel.Name ?? (object)DBNull.Value);
            insertCommand.Parameters.AddWithValue("@PrivateKey", SSHKeyProtection.Protect(enrichedModel.PrivateKey) ?? (object)DBNull.Value);
            insertCommand.Parameters.AddWithValue("@PublicKey", enrichedModel.PublicKey ?? (object)DBNull.Value);
            insertCommand.Parameters.AddWithValue("@Fingerprint", enrichedModel.Fingerprint ?? (object)DBNull.Value);
            insertCommand.Parameters.AddWithValue("@CreatedAt", enrichedModel.CreatedAt ?? (object)DBNull.Value);
            insertCommand.ExecuteNonQuery();

            insertCommand.CommandText = "SELECT last_insert_rowid()";
            insertCommand.Parameters.Clear();
            return Convert.ToInt32(insertCommand.ExecuteScalar());
        }

        // 获取所有SSH密钥列表
        public List<SSHKeyModel> QuerySSHKeys()
        {
            List<SSHKeyModel> entries = new List<SSHKeyModel>();

            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var queryCommand = connection.CreateCommand();
                queryCommand.CommandText = "SELECT Id, Name, PrivateKey, PublicKey, Fingerprint, CreatedAt FROM SSHKeyTable ORDER BY Id";

                using (SqliteDataReader reader = queryCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        entries.Add(ReadSSHKeyModel(reader, false));
                    }
                }
            }

            return entries;
        }

        // 根据ID获取SSH密钥（包含私钥）
        public SSHKeyModel GetSSHKeyById(int id)
        {
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT Id, Name, PrivateKey, PublicKey, Fingerprint, CreatedAt FROM SSHKeyTable WHERE Id = @Id";
                selectCommand.Parameters.AddWithValue("@Id", id);

                using (SqliteDataReader reader = selectCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return ReadSSHKeyModel(reader, true);
                    }
                }
            }

            return null;
        }

        // 删除SSH密钥
        public void DeleteSSHKey(int id)
        {
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = "DELETE FROM SSHKeyTable WHERE Id = @Id";
                deleteCommand.Parameters.AddWithValue("@Id", id);
                deleteCommand.ExecuteNonQuery();

                // 清除引用了该密钥的服务器记录
                var clearCommand = connection.CreateCommand();
                clearCommand.CommandText = "UPDATE CMSTable SET SSHKeyId = NULL WHERE SSHKeyId = @SSHKeyId";
                clearCommand.Parameters.AddWithValue("@SSHKeyId", id.ToString());
                clearCommand.ExecuteNonQuery();
            }
        }

        private static SSHKeyModel ReadSSHKeyModel(SqliteDataReader reader, bool includePrivateKey)
        {
            return new SSHKeyModel
            {
                Id = reader.GetInt32(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                PrivateKey = includePrivateKey && !reader.IsDBNull(2) ? SSHKeyProtection.Unprotect(reader.GetString(2)) : "",
                PublicKey = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Fingerprint = reader.IsDBNull(4) ? "" : reader.GetString(4),
                CreatedAt = reader.IsDBNull(5) ? "" : reader.GetString(5)
            };
        }

        private static SSHKeyModel EnsureSSHKeyMetadata(SSHKeyModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.PublicKey) && !string.IsNullOrWhiteSpace(model.Fingerprint))
            {
                return model;
            }

            SSHKeyModel enrichedModel = SSHKeyMethod.CreateSSHKeyModel(model.Name, model.PrivateKey);
            if (!string.IsNullOrWhiteSpace(model.CreatedAt))
            {
                enrichedModel.CreatedAt = model.CreatedAt;
            }

            return enrichedModel;
        }

        // 迁移现有的SSHKey路径到SSHKeyTable中
        private static void MigrateSSHKeyPaths(SqliteConnection connection)
        {
            List<string> keyPaths = new List<string>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT DISTINCT SSHKey FROM CMSTable WHERE SSHKey IS NOT NULL AND SSHKey <> '' AND (SSHKeyId IS NULL OR SSHKeyId = '')";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string keyPath = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        if (!string.IsNullOrWhiteSpace(keyPath) && !int.TryParse(keyPath, out _))
                        {
                            keyPaths.Add(keyPath);
                        }
                    }
                }
            }

            foreach (string keyPath in keyPaths)
            {
                try
                {
                    if (!File.Exists(keyPath))
                    {
                        continue;
                    }

                    string privateKey = File.ReadAllText(keyPath);
                    if (string.IsNullOrWhiteSpace(privateKey))
                    {
                        continue;
                    }

                    int sshKeyId = InsertSSHKey(connection, new SSHKeyModel
                    {
                        Name = Path.GetFileName(keyPath),
                        PrivateKey = privateKey,
                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });

                    using (var updateCommand = connection.CreateCommand())
                    {
                        updateCommand.CommandText = "UPDATE CMSTable SET SSHKeyId = @SSHKeyId WHERE SSHKey = @SSHKeyPath AND (SSHKeyId IS NULL OR SSHKeyId = '')";
                        updateCommand.Parameters.AddWithValue("@SSHKeyId", sshKeyId.ToString());
                        updateCommand.Parameters.AddWithValue("@SSHKeyPath", keyPath);
                        updateCommand.ExecuteNonQuery();
                    }
                }
                catch
                {
                    // 如果旧路径不可读，保留配置，用户可在编辑时重新导入密钥。
                }
            }
        }

        // 回填SSHKey元数据（公钥、指纹）
        private static void BackfillSSHKeyMetadata(SqliteConnection connection)
        {
            List<SSHKeyModel> keys = new List<SSHKeyModel>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, Name, PrivateKey, PublicKey, Fingerprint, CreatedAt FROM SSHKeyTable WHERE PrivateKey IS NOT NULL AND PrivateKey <> '' AND (PublicKey IS NULL OR PublicKey = '' OR Fingerprint IS NULL OR Fingerprint = '')";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        try
                        {
                            keys.Add(ReadSSHKeyModel(reader, true));
                        }
                        catch
                        {
                            // 加密内容损坏时跳过回填，不影响其他密钥使用。
                        }
                    }
                }
            }

            foreach (SSHKeyModel key in keys)
            {
                try
                {
                    SSHKeyModel enrichedKey = SSHKeyMethod.CreateSSHKeyModel(key.Name, key.PrivateKey);
                    using (var updateCommand = connection.CreateCommand())
                    {
                        updateCommand.CommandText = "UPDATE SSHKeyTable SET PublicKey = @PublicKey, Fingerprint = @Fingerprint WHERE Id = @Id";
                        updateCommand.Parameters.AddWithValue("@Id", key.Id);
                        updateCommand.Parameters.AddWithValue("@PublicKey", enrichedKey.PublicKey ?? (object)DBNull.Value);
                        updateCommand.Parameters.AddWithValue("@Fingerprint", enrichedKey.Fingerprint ?? (object)DBNull.Value);
                        updateCommand.ExecuteNonQuery();
                    }
                }
                catch
                {
                    // 旧数据无法解析时保留原记录，用户可删除后重新导入。
                }
            }
        }

        private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string columnType)
        {
            bool columnExists = false;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info({tableName})";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            columnExists = true;
                            break;
                        }
                    }
                }
            }

            if (!columnExists)
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}";
                    command.ExecuteNonQuery();
                }
            }
        }

        #endregion
    }
}
