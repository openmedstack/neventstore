namespace OpenMedStack.NEventStore.Persistence.Sql;

using System;
using System.Security.Cryptography;
using System.Text;

public class Sha256StreamIdHasher : IStreamIdHasher
{
    public string GetHash(string streamId)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(streamId));
        return BitConverter.ToString(hashBytes).Replace("-", "");
    }
}
