using System;
using System.Security.Cryptography;

namespace DeSwitcher.Core
{
    public static class Signature
    {
        public const string PublicKeyXml = "<RSAKeyValue><Modulus>miYBZEH1FaXqbccodS2d5xCfmuZVUBzSffAhAALolkM5lXf6Nvig72XEgqaK8ITX6EmFK2dZOG5hxJ7Ps97BvZOUXKuLIgLPiwVLIfj27iU3XS3dk9hAFS2KXvzgFB4cRQiRzBmdAsbY+37JWre1o3eoR8cvJs2p6GfN9dCPoE3sL5RYAKgIO7xRWTX9vWOJ6HZTFxv/imWYAJtdziJCrTYJwqiUQctvqVuUPBOagQNpX9hEOKsRzDGXD4z97n9SmASbbSocfjY2T2UMEIeuzvZjJcjuU6o697CLAPExK9xPZI4RZQtMnDn+h3cAku2RaKIPEquRJaJaDbY63DcboZWY+0PHYA9+jImAIKIO8hBjSXKSb1Foba8Ubav6VL7OVpOv5WmkQGAkTkMwnPBxGSO+KcvGCtyuVZxUcMs1C/+Y2k7nTfRoG19FSxoO2SGDbpujAeX8uA6UFE592V+EJvImjM+GheJf6Z0jBg4P1paW3DDwMsuLDpLDm1sqPsIh</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        public static bool Verify(byte[] data, string base64Signature) => Verify(data, base64Signature, PublicKeyXml);

        internal static bool Verify(byte[] data, string base64Signature, string publicKeyXml)
        {
            try
            {
                byte[] sig = Convert.FromBase64String(base64Signature.Trim());
                byte[] hash;
                using (var sha = SHA256.Create())
                    hash = sha.ComputeHash(data);
                using (var rsa = RSA.Create())
                {
                    rsa.FromXmlString(publicKeyXml);
                    var deformatter = new RSAPKCS1SignatureDeformatter(rsa);
                    deformatter.SetHashAlgorithm("SHA256");
                    return deformatter.VerifySignature(hash, sig);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
