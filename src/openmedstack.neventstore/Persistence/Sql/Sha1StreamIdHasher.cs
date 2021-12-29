namespace OpenMedStack.NEventStore.Persistence.Sql
{
    using System;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;

    public class Sha1StreamIdHasher : IStreamIdHasher
    {
        public string GetHash(string streamId)
        {
            var hashBytes = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(streamId));
            return BitConverter.ToString(hashBytes).Replace("-", "");
        }
    }

    public class StreamIdHasher<THash> : IStreamIdHasher
        where THash : HashAlgorithm
    {
        private static readonly MethodInfo CreateMethod = typeof(THash).GetMethod(
            "Create",
            BindingFlags.Public | BindingFlags.Static,
            null,
            CallingConventions.Any,
            Type.EmptyTypes,
            Array.Empty<ParameterModifier>())!;

        public string GetHash(string streamId)
        {
            var instance = (THash)CreateMethod.Invoke(null, null)!;
            var hash = instance.ComputeHash(Encoding.UTF8.GetBytes(streamId));

            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }
    }
}