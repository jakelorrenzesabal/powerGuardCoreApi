using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace PowerGuardCoreApi._Helpers
{
    public interface IDeviceKeyHelper
    {
        string GenerateDeviceKey();
        string EncryptDeviceKey(string plainText);
        string DecryptDeviceKey(string blobBase64);
        string BcryptHash(string plain);
        bool BcryptCompare(string plain, string hash);
        bool VerifyDeviceKey(string presentedPlain, string storedBlob, string storedHash);
    }

    public class DeviceKeyHelper : IDeviceKeyHelper
    {
        private const int PBKDF2_ITER = 150_000;
        private const int SALT_LEN = 16;
        private const int IV_LEN = 12;
        private const int KEY_LEN = 32;
        private const int GCM_TAG_LEN = 16;
        private const int HMAC_LEN = 32;

        private readonly IConfiguration _config;

        public DeviceKeyHelper(IConfiguration config)
        {
            _config = config;
        }

        private string GetSecret()
        {
            var secret = _config["Secret"];
            if (string.IsNullOrEmpty(secret))
                throw new Exception("config:Secret missing - required for device key derivation");
            return secret;
        }

        // pattern: &&xxxxxx%^NN
        public string GenerateDeviceKey()
        {
            var raw = new byte[8];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(raw);
            var base64 = Convert.ToBase64String(raw);
            var letters = new StringBuilder();
            foreach (var c in base64)
            {
                if (char.IsLetterOrDigit(c)) letters.Append(char.ToLower(c));
                if (letters.Length == 6) break;
            }
            var digits = RandomNumberGenerator.GetInt32(10, 100).ToString("D2");
            return $"&&{letters}%^{digits}";
        }

        private (byte[] encKey, byte[] hmacKey) DeriveKeys(string secret, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(secret, salt, PBKDF2_ITER, HashAlgorithmName.SHA512);
            var master = pbkdf2.GetBytes(KEY_LEN);

            var encInfo = Encoding.UTF8.GetBytes("device-key-enc");
            var hmacInfo = Encoding.UTF8.GetBytes("device-key-hmac");

            using var hmacEnc = new HMACSHA256(master);
            using var hmacHmac = new HMACSHA256(master);

            var encH = hmacEnc.ComputeHash(encInfo);
            var hmacH = hmacHmac.ComputeHash(hmacInfo);

            var encKey = new byte[KEY_LEN];
            var hmacKey = new byte[KEY_LEN];
            Array.Copy(encH, 0, encKey, 0, Math.Min(encH.Length, KEY_LEN));
            Array.Copy(hmacH, 0, hmacKey, 0, Math.Min(hmacH.Length, KEY_LEN));
            return (encKey, hmacKey);
        }

        public string EncryptDeviceKey(string plainText)
        {
            var salt = RandomNumberGenerator.GetBytes(SALT_LEN);
            var (encKey, hmacKey) = DeriveKeys(GetSecret(), salt);
            var iv = RandomNumberGenerator.GetBytes(IV_LEN);

            byte[] ciphertext, tag;
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            ciphertext = new byte[plainBytes.Length];
            tag = new byte[GCM_TAG_LEN];
            using (var aes = new AesGcm(encKey, GCM_TAG_LEN))
            {
                aes.Encrypt(iv, plainBytes, ciphertext, tag);
            }

            var hmacInput = new byte[iv.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(iv, 0, hmacInput, 0, iv.Length);
            Buffer.BlockCopy(tag, 0, hmacInput, iv.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, hmacInput, iv.Length + tag.Length, ciphertext.Length);

            using var hmac = new HMACSHA256(hmacKey);
            var hmacVal = hmac.ComputeHash(hmacInput);

            var blob = new byte[salt.Length + iv.Length + tag.Length + ciphertext.Length + hmacVal.Length];
            int offset = 0;
            Buffer.BlockCopy(salt, 0, blob, offset, salt.Length); offset += salt.Length;
            Buffer.BlockCopy(iv, 0, blob, offset, iv.Length); offset += iv.Length;
            Buffer.BlockCopy(tag, 0, blob, offset, tag.Length); offset += tag.Length;
            Buffer.BlockCopy(ciphertext, 0, blob, offset, ciphertext.Length); offset += ciphertext.Length;
            Buffer.BlockCopy(hmacVal, 0, blob, offset, hmacVal.Length);

            return Convert.ToBase64String(blob);
        }

        public string DecryptDeviceKey(string blobBase64)
        {
            var buffer = Convert.FromBase64String(blobBase64);
            var minLen = SALT_LEN + IV_LEN + GCM_TAG_LEN + HMAC_LEN;
            if (buffer.Length < minLen) throw new Exception("Invalid encrypted device key format");

            var salt = buffer.AsSpan(0, SALT_LEN).ToArray();
            var iv = buffer.AsSpan(SALT_LEN, IV_LEN).ToArray();
            var tag = buffer.AsSpan(SALT_LEN + IV_LEN, GCM_TAG_LEN).ToArray();
            var hmacStart = buffer.Length - HMAC_LEN;
            var ciphertext = buffer.AsSpan(SALT_LEN + IV_LEN + GCM_TAG_LEN, hmacStart - (SALT_LEN + IV_LEN + GCM_TAG_LEN)).ToArray();
            var hmacVal = buffer.AsSpan(hmacStart, HMAC_LEN).ToArray();

            var (encKey, hmacKey) = DeriveKeys(GetSecret(), salt);

            var hmacInput = new byte[iv.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(iv, 0, hmacInput, 0, iv.Length);
            Buffer.BlockCopy(tag, 0, hmacInput, iv.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, hmacInput, iv.Length + tag.Length, ciphertext.Length);

            using var hmac = new HMACSHA256(hmacKey);
            var expectedHmac = hmac.ComputeHash(hmacInput);
            if (!CryptographicOperations.FixedTimeEquals(expectedHmac, hmacVal))
                throw new Exception("HMAC verification failed for device key");

            using var aes = new AesGcm(encKey, GCM_TAG_LEN);
            var plainBytes = new byte[ciphertext.Length];
            aes.Decrypt(iv, ciphertext, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }

        public string BcryptHash(string plain) =>
            BCrypt.Net.BCrypt.HashPassword(plain, workFactor: 12);

        public bool BcryptCompare(string plain, string hash) =>
            !string.IsNullOrEmpty(plain) && !string.IsNullOrEmpty(hash) &&
            BCrypt.Net.BCrypt.Verify(plain, hash);

        public bool VerifyDeviceKey(string presentedPlain, string storedBlob, string storedHash)
        {
            if (!string.IsNullOrEmpty(storedHash))
            {
                try { if (BcryptCompare(presentedPlain, storedHash)) return true; }
                catch { }
            }
            try
            {
                var real = DecryptDeviceKey(storedBlob);
                var a = Encoding.UTF8.GetBytes(real);
                var b = Encoding.UTF8.GetBytes(presentedPlain);
                if (a.Length != b.Length) return false;
                return CryptographicOperations.FixedTimeEquals(a, b);
            }
            catch { return false; }
        }
    }
}