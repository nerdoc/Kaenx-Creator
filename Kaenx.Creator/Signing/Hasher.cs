using System;
using System.IO;
using System.Security.Cryptography;



namespace Kaenx.Creator.Signing;

class Hasher
{
    public static byte[] buildEts5Hash(byte[] origHash, int offset, int mask)
    {
        byte[] etsHash = new byte[20];
        int m0 = mask >> 8;
        int m1 = mask & 255;
        int i = 0;
        while(i < origHash.Length)
        {
            etsHash[i] = Convert.ToByte(((origHash[i] + offset) % 256) ^ m0);
            i = i+1;
            etsHash[i] = Convert.ToByte(((origHash[i] + offset) % 256) ^ m1);
            i = i+1;
        }
        return etsHash;
    }

    public static byte[] getFileHash(string path)
    {
        if(!File.Exists(path))
            throw new FileNotFoundException("File not found: " + path);

        byte[] file = File.ReadAllBytes(path);

        SHA1 sha1 = SHA1.Create();
        
        return sha1.ComputeHash(file);
    }

    public static (byte[], byte[]) readHashes(string path)
    {
        string hashFile = path + ".ets5hash";
        if(!File.Exists(hashFile))
            throw new FileNotFoundException("File not found: " + path);

        string hash = File.ReadAllText(hashFile);
        byte[] etsHash = Convert.FromBase64String(hash);

        if(etsHash.Length != 20)
            throw new Exception($"Hash {hashFile} file has unexpected length: {etsHash.Length}" );

        return (getFileHash(path), etsHash);
    }

    public static (int, int) findOffsetMask(byte[] origHash, byte[] etsHash, int offset = 0)
    {
        while(offset < 256)
        {
            int[] shifted = new int[4] {
                (origHash[0] + offset) % 256,
                (origHash[1] + offset) % 256,
                (origHash[2] + offset) % 256,
                (origHash[3] + offset) % 256
            };

            int m0 = shifted[0] ^ etsHash[0];
            int m1 = shifted[1] ^ etsHash[1];
            int m2 = shifted[2] ^ etsHash[2];
            int m3 = shifted[3] ^ etsHash[3];
            if(m0 == m2 && m1 == m3)
            {
                // Candidate found, validate whole hash
                int mask = m0 << 8 | m1;
                if(validateHash(origHash, etsHash, offset, mask))
                    return (offset, mask);
            }
            offset = offset + 1;
        }

        return (-1, -1);
    }

    public static bool validateHash(byte[] origHash, byte[] etsHash, int offset, int mask)
    {
        int i = 0;
        int m0 = mask >> 8;
        int m1 = mask & 255;
        while(i < origHash.Length)
        {
            int a0 = (origHash[i] + offset) % 256;
            int b0 = etsHash[i];
            i = i+1;
            int a1 = (origHash[i] + offset) % 256;
            int b1 = etsHash[i];
            i = i+1;
            if((a0 ^ b0) != m0 || (a1 ^ b1) != m1)
                return false;
        }
        return true;
    }
}

