using System.Text;

namespace Asahi.BotEmoteManagement.SourceGenerators;
// edited to work with netstandard2.0 (aka manual version of runes)

/// <summary>
/// Base2048 is a binary-to-text encoding optimized for transmitting data
/// through Twitter.
/// </summary>
public static class Base2048Converter
{
    // Z is a number, usually a uint11 but sometimes a uint3

    private const int BITS_PER_CHAR = 11; // Base2048 is an 11-bit encoding
    private const int BITS_PER_BYTE = 8;

    // Compressed representation of inclusive ranges of characters used in this encoding.
    private static readonly string[] pairStrings =
    [
        @"89AZazÆÆÐÐØØÞßææððøøþþĐđĦħııĸĸŁłŊŋŒœŦŧƀƟƢƮƱǃǝǝǤǥǶǷȜȝȠȥȴʯͰͳͶͷͻͽͿͿΑΡΣΩαωϏϏϗϯϳϳϷϸϺϿЂЂЄІЈЋЏИКикяђђєіјћџѵѸҁҊӀӃӏӔӕӘәӠӡӨөӶӷӺԯԱՖաֆאתװײؠءاؿفي٠٩ٮٯٱٴٹڿہہۃےەەۮۼۿۿܐܐܒܯݍޥޱޱ߀ߪࠀࠕࡀࡘࡠࡪࢠࢴࢶࢽऄनपरलळवहऽऽॐॐॠॡ०९ॲঀঅঌএঐওনপরললশহঽঽৎৎৠৡ০ৱ৴৹ৼৼਅਊਏਐਓਨਪਰਲਲਵਵਸਹੜੜ੦੯ੲੴઅઍએઑઓનપરલળવહઽઽૐૐૠૡ૦૯ૹૹଅଌଏଐଓନପରଲଳଵହଽଽୟୡ୦୯ୱ୷ஃஃஅஊஎஐஒஓககஙசஜஜஞடணதநபமஹௐௐ௦௲అఌఎఐఒనపహఽఽౘౚౠౡ౦౯౸౾ಀಀಅಌಎಐಒನಪಳವಹಽಽೞೞೠೡ೦೯ೱೲഅഌഎഐഒഺഽഽൎൎൔൖ൘ൡ൦൸ൺൿඅඖකනඳරලලවෆ෦෯กะาาเๅ๐๙ກຂຄຄງຈຊຊຍຍດທນຟມຣລລວວສຫອະາາຽຽເໄ໐໙ໞໟༀༀ༠༳ཀགངཇཉཌཎདནབམཛཝཨཪཬྈྌကဥဧဪဿ၉ၐၕ",
        @"07"
    ];
    
    private static readonly Dictionary<int, List<string>> LookupE = new();
    private static readonly Dictionary<string, (int numZBits, int z)> LookupD = new();

    static Base2048Converter()
    {
        for (var r = 0; r < pairStrings.Length; r++)
        {
            var pairString = pairStrings[r];
            
            // Decompression
            var encodeRepertoire = new List<string>();
            var runes = new List<int>(pairString.Length);
            for (var i = 0; i < pairString.Length; i++)
            {
                if (char.IsHighSurrogate(pairString[i]))
                {
                    runes.Add(char.ConvertToUtf32(pairString[i], pairString[i + 1]));
                    i++;
                }
                else
                {
                    runes.Add(pairString[i]);
                }
            }
            // enon: again, pairStrings is a pair of two characters, and we want every character between those two characters.
            for (var i = 0; i < runes.Count; i += 2)
            {
                var first = runes[i];
                var last = runes[i + 1];
                for (var codePoint = first; codePoint <= last; codePoint++)
                {
                    encodeRepertoire.Add(char.ConvertFromUtf32(codePoint));
                }
            }
            
            var numZBits = BITS_PER_CHAR - BITS_PER_BYTE * r; // 0 -> 11, 1 -> 3
            LookupE[numZBits] = encodeRepertoire;
            for (int z = 0; z < encodeRepertoire.Count; z++)
            {
                var chr = encodeRepertoire[z];
                LookupD[chr] = (numZBits, z);
            }
        }
    }

    public static string Encode(byte[] uint8Array)
    {
        var length = uint8Array.Length;

        var str = new StringBuilder();
        var z = 0;
        var numZBits = 0;

        for (int i = 0; i < length; i++)
        {
            var uint8 = uint8Array[i];
            
            // Take most significant bit first
            for (var j = BITS_PER_BYTE - 1; j >= 0; j--)
            {
                var bit = (uint8 >> j) & 1;
                
                z = (z << 1) + bit;
                numZBits++;
                
                if (numZBits == BITS_PER_CHAR)
                {
                    str.Append(LookupE[numZBits][z]);
                    z = 0;
                    numZBits = 0;
                }
            }
        }

        if (numZBits != 0)
        {
            // Final bits require special treatment.

            // byte = bbbcccccccc, numBits = 11, padBits = 0
            // byte = bbcccccccc, numBits = 10, padBits = 1
            // byte = bcccccccc, numBits = 9, padBits = 2
            // byte = cccccccc, numBits = 8, padBits = 3
            // byte = ccccccc, numBits = 7, padBits = 4
            // byte = cccccc, numBits = 6, padBits = 5
            // byte = ccccc, numBits = 5, padBits = 6
            // byte = cccc, numBits = 4, padBits = 7
            // => Pad `byte` out to 11 bits using 1s, then encode as normal (repertoire 0)

            // byte = ccc, numBits = 3, padBits = 0
            // byte = cc, numBits = 2, padBits = 1
            // byte = c, numBits = 1, padBits = 2
            // => Pad `byte` out to 3 bits using 1s, then encode specially (repertoire 1)

            while (!LookupE.ContainsKey(numZBits))
            {
                z = (z << 1) + 1;
                numZBits++;
            }
            
            str.Append(LookupE[numZBits][z]);
        }
        
        return str.ToString();
    }

    public static byte[] Decode(string str)
    {
        var runes = new List<int>(str.Length);
        for (var i = 0; i < str.Length; i++)
        {
            if (char.IsHighSurrogate(str[i]))
            {
                runes.Add(char.ConvertToUtf32(str[i], str[i + 1]));
                i++;
            }
            else
            {
                runes.Add(str[i]);
            }
        }
        
        var length = runes.Count;
        
        // This length is a guess. There's a chance we allocate one more byte here
        // than we actually need. But we can count and slice it off later
        var uint8Array = new byte[length * BITS_PER_CHAR / BITS_PER_BYTE];
        var numUint8s = 0;
        var uint8 = 0;
        var numUint8Bits = 0;

        for (int i = 0; i < length; i++)
        {
            var chr = runes[i].ToString();

            if (!LookupD.TryGetValue(chr, out var value))
            {
                throw new InvalidDataException($"Unrecognised Base2048 character: {chr}");
            }

            var (numZBits, z) = value;

            if (numZBits != BITS_PER_CHAR && i != length - 1)
            {
                throw new InvalidDataException($"Secondary character found before end of input at position {i}");
            }
            
            // Take most significant bit first
            for (var j = numZBits - 1; j >= 0; j--)
            {
                var bit = (z >> j) & 1;
                
                uint8 = (uint8 << 1) + bit;
                numUint8Bits++;

                if (numUint8Bits == BITS_PER_BYTE)
                {
                    uint8Array[numUint8s] = (byte)uint8;
                    numUint8s++;
                    uint8 = 0;
                    numUint8Bits = 0;
                }
            }
        }
        
        // Final padding bits! Requires special consideration!
        // Remember how we always pad with 1s?
        // Note: there could be 0 such bits, check still works though
        if (uint8 != ((1 << numUint8Bits) - 1))
        {
            throw new InvalidDataException("Padding mismatch");
        }

        var newArray = new byte[numUint8s];
        Array.Copy(uint8Array, newArray, numUint8s);
        return newArray;
    }
}
