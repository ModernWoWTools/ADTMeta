using DBCD.Providers;

namespace ADTMeta.Provider
{
    class CASCDBCProvider : IDBCProvider
    {
        public Stream StreamForTableName(string tableName, string build)
        {
            if (!ListFile.FileIdMap.TryGetValue($"dbfilesclient/{tableName.ToLower()}.db2", out var fileDataID))
                throw new FileNotFoundException("Could not find " + tableName);

            if (!CASC.Instance.FileExists(fileDataID))
                throw new FileNotFoundException("Could not find " + fileDataID);

            return CASC.Instance.OpenFile(fileDataID);
        }
    }
}
