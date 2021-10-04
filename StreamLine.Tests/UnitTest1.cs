using System;
using System.IO;
using System.Text;
using Xunit;
using System.Linq;

using StreamLine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StreamLine.Tests;

public class UnitTest1
{
    private readonly byte[] TestString1 = Encoding.UTF8.GetBytes("abcde\nabcdefghil\n");

    public static IEnumerable<object[]> GetGoodStrings() {
      var strings = new string[] {
        "", "\n", "\n\n", "\nafa", "\nafa\n", "afb", "a",
        "\nabcd\nefkl\nght", "\nabcd\nefkl\nght\n", "aaa  ","💩",
        "\r\n", "\r\n\r\n", "\r\nafa", "\r\nafa\r\n",
        "\r\nabcd\r\nefkl\r\nght", "\r\nabcd\r\nefkl\r\nght\r\n",
      };
      var bufferLength = new int[] { 2, 10, 50, 100};

      foreach(var s in strings)
        foreach(var i in bufferLength)
          yield return new object[] {s, i};  
    }
    // https://stackoverflow.com/questions/1547252/how-do-i-concatenate-two-arrays-in-c
    public static T[] Concat<T>(T[][] arrays)
    {
        // return (from array in arrays from arr in array select arr).ToArray();

        var result = new T[arrays.Sum(a => a.Length)];
        int offset = 0;
        for (int x = 0; x < arrays.Length; x++)
        {
            arrays[x].CopyTo(result, offset);
            offset += arrays[x].Length;
        }
        return result;
    }

    private (byte[], byte[]) RunTest(string s, int bufferLength) {
      // Concatenating all lines
      var result = UnitTest1.Concat<byte>(Regex.Split(s, "\r\n|\r|\n").Select(k => Encoding.UTF8.GetBytes(k)).ToArray()); 

      var testString = Encoding.UTF8.GetBytes(s);
      var readStream = new MemoryStream(testString);
      var writeStream = new MemoryStream();

      Span<byte> buffer = stackalloc byte[bufferLength];
      ByteLiner l = new(readStream, buffer);
      Span<byte> span = stackalloc byte[0]; // https://github.com/dotnet/roslyn/issues/53014

      while((span = l.ReadLine()) != null) {
        writeStream.Write(span);
      }
      return (result, writeStream.ToArray());
    }
    
    [Fact]
    public void ThrowIfWrongParams()
    {
      var readStream = new MemoryStream(TestString1);
      var buffer = new byte[3];
      Assert.Throws<ArgumentException>(() => new ByteLiner(null!, buffer));
      Assert.Throws<ArgumentException>(() => new ByteLiner(readStream, null!));
      Assert.Throws<ArgumentException>(() => new ByteLiner(readStream, new byte[0]));
    }

    [Theory]
    [MemberData(nameof(StreamLine.Tests.UnitTest1.GetGoodStrings), MemberType = typeof(StreamLine.Tests.UnitTest1))]
    public void CanProcessGoodStrings(String s, int l)
    {
      var maxLineLength = s.Split("\n").Select(s => Encoding.UTF8.GetByteCount(s)).Max();
      if(maxLineLength > l) {
        Assert.Throws<Exception>( () => RunTest(s, l));
      } else {
        var (s1, s2) = RunTest(s, l);
        Assert.NotNull(s1);
        Assert.NotNull(s2);
        Assert.Equal(s1, s2);
      }
    }
}
