namespace OpenMedStack.NEventStore.Persistence.Sql;

using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

public class Sha1StreamIdHasher : IStreamIdHasher
{
    public string GetHash(string streamId)
    {
        var hashBytes = SHA1 .Create().ComputeHash(Encoding.UTF8.GetBytes(streamId));
        return BitConverter.ToString(hashBytes).Replace("-", "");
    }
}
