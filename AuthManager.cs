using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth
{
    [BismuthManagerInfo("BISMUTH_AUTH", "Auth Manager", "Handles authorization and auth methods")]
    public class AuthManager : BismuthGenericManager
    {
        static Dictionary<string, AuthType> authMethods = new Dictionary<string, AuthType>(StringComparer.OrdinalIgnoreCase);

        public override bool Setup()
        {
            AddAuthMethod("basic", new AuthType_Basic());
            AddAuthMethod("md5", new AuthType_MD5());
            AddAuthMethod("sha1", new AuthType_SHA1());
            AddAuthMethod("sha256", new AuthType_SHA256());
            AddAuthMethod("sha384", new AuthType_SHA384());
            AddAuthMethod("sha512", new AuthType_SHA512());
            return true;
        }

        public static void AddAuthMethod(string method, AuthType type)
        {
            if (authMethods.ContainsKey(method))
            {
                LogManager.Warn("Auth Manager", "Auth method " + method + " was overridden");
                authMethods[method] = type;
            }
            else
            {
                authMethods.Add(method, type);
            }
        }

        public static bool CheckPlaintextCredentials(string method, string plainText, string hashedText)
        {
            if (!authMethods.ContainsKey(method))
                return false;

            return authMethods[method].ComparePlaintext(plainText, hashedText);
        }

        public static string GetEncryptedText(string method, string plainText)
        {
            if (!authMethods.ContainsKey(method))
                return plainText;

            return authMethods[method].Encrypt(plainText);
        }
    }

    public abstract class AuthType
    {
        public virtual bool ComparePlaintext(string plainText, string hashedText) { return false; }
        public virtual string Encrypt(string plainText) { return plainText; }
    }

    public class AuthType_Basic : AuthType
    {
        public override bool ComparePlaintext(string plainText, string hashedText) { return plainText == hashedText; }
    }


    public abstract class AuthType_GenericHash : AuthType
    {
        protected HashAlgorithm hashAlgorithm = null;

        public override bool ComparePlaintext(string plainText, string hashedText)
        {
            plainText = Encrypt(plainText);
            return plainText == hashedText.ToUpper();
        }

        public override string Encrypt(string plainText)
        {
            byte[] hash = hashAlgorithm.ComputeHash(Encoding.ASCII.GetBytes(plainText));
            StringBuilder finalHash = new StringBuilder();
            for (int i = 0; i < hash.Length; ++i) finalHash.Append(hash[i].ToString("X2"));
            return finalHash.ToString();
        }
    }

    public class AuthType_MD5 : AuthType_GenericHash { public AuthType_MD5() { hashAlgorithm = MD5.Create(); } }
    public class AuthType_SHA1 : AuthType_GenericHash { public AuthType_SHA1() { hashAlgorithm = SHA1.Create(); } }
    public class AuthType_SHA256 : AuthType_GenericHash { public AuthType_SHA256() { hashAlgorithm = SHA256.Create(); } }
    public class AuthType_SHA384 : AuthType_GenericHash { public AuthType_SHA384() { hashAlgorithm = SHA384.Create(); } }
    public class AuthType_SHA512 : AuthType_GenericHash { public AuthType_SHA512() { hashAlgorithm = SHA512.Create(); } }
}
