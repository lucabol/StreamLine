using static System.Diagnostics.Trace;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Order;

using StreamLine;
using System.Text;
using System.Text.RegularExpressions;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;

[MemoryDiagnoser, Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ReadlineBench {

  [Params(1000, 100_000, 1_000_000)]
  public int ItemCount;

  public int BufferLength = 256;

  Stream memoryStream = null!;
  readonly static Encoding utf8 = new UTF8Encoding();

  static T[] Concat<T>(T[][] arrays)
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

  Stream CreateStreamOfItems()
  {
        var stream = new MemoryStream();

        using var writer = new StreamWriter(stream, new UTF8Encoding(), leaveOpen: true);

        for (var i = 1; i < ItemCount; i++)
        {
            writer.Write($"Item {i},\n");
            writer.Flush();
        }

        writer.Write($"Item {ItemCount}");
        writer.Flush();

        stream.Seek(0, SeekOrigin.Begin);

        return stream;
    }

  [GlobalSetup]
  public void GlobalSetup() {
    memoryStream = CreateStreamOfItems();
    var i = MyReadline();
    memoryStream = CreateStreamOfItems();
    var j = StandardReadLine();
    Assert(i == j);
  }

  [IterationSetup]
  public void Setup() {
    memoryStream = CreateStreamOfItems();
  }

  [Benchmark(Baseline = true)]
  public int MyReadlineRaw() {

    Span<byte> buffer = stackalloc byte[BufferLength];
    Liner l = new(memoryStream, buffer);
    Span<byte> span = stackalloc byte[0]; // https://github.com/dotnet/roslyn/issues/53014

    int sum = 0;
    while((span = l.ReadLine()) != null) {
      //sum += utf8.GetCharCount(span);
      sum += span.Length;
    }
    return sum;
  }
  [Benchmark]
  public int MyReadline() {

    Span<byte> buffer = stackalloc byte[BufferLength];
    Liner l = new(memoryStream, buffer);
    Span<byte> span = stackalloc byte[0]; // https://github.com/dotnet/roslyn/issues/53014

    int sum = 0;
    while((span = l.ReadLine()) != null) {
      sum += utf8.GetCharCount(span);
    }
    return sum;
  }

  [Benchmark]
  public int StandardReadLine() {
    StreamReader reader = new(memoryStream, utf8, false, BufferLength);

      int sum = 0;
      string? s;
      while((s = reader.ReadLine()) != null) {
        sum += s.Length;
      }
      return sum;
  }

  private int ReadLastItem(in ReadOnlySequence<byte> sequence)
  {
      var length = (int)sequence.Length;
      var bytes = 0;

      // Could just return length but simulate reading
      if (length < BufferLength) // if the item is small enough we'll stack allocate the buffer
      {
          Span<byte> byteBuffer = stackalloc byte[length];
          sequence.CopyTo(byteBuffer);
          bytes += length;
      }
      else // otherwise we'll rent an array to use as the buffer
      {
          var byteBuffer = ArrayPool<byte>.Shared.Rent(length);

          try
          {
              sequence.CopyTo(byteBuffer);
              bytes += length;
          }
          finally
          {
              ArrayPool<byte>.Shared.Return(byteBuffer);
          }
      }

      return bytes;
  }
  private (SequencePosition, int) ReadItems(in ReadOnlySequence<byte> sequence, bool isCompleted)
  {
        var reader = new SequenceReader<byte>(sequence);

        int bytes = 0;
        while (!reader.End) // loop until we've read the entire sequence
        {
            if (reader.TryReadTo(out ReadOnlySpan<byte> itemBytes, (byte)'\n', advancePastDelimiter: true)) // we have an item to handle
            {
              var len = itemBytes.Length;
              bytes += len > 1 && itemBytes[len - 1] == (byte)'\r' ? len - 1 : len;
            }
            else if (isCompleted) // read last item which has no final delimiter
            {
                bytes += ReadLastItem(sequence.Slice(reader.Position));
                reader.Advance(sequence.Length); // advance reader to the end
            }
            else // no more items in this sequence
            {
                break;
            }
        }

        return (reader.Position, bytes);
    }

  [Benchmark]
  async public Task<int> PipeReadline() {
      var pipeReader = PipeReader.Create(memoryStream, new StreamPipeReaderOptions(bufferSize: BufferLength));

      var bytes = 0;
      while (true)
      {
          var result = await pipeReader.ReadAsync(); // read from the pipe

          var buffer = result.Buffer;

          var (position, moreBytes) = ReadItems(buffer, result.IsCompleted); // read complete items from the current buffer
          bytes += moreBytes;

          if (result.IsCompleted) 
              break; // exit if we've read everything from the pipe

          pipeReader.AdvanceTo(position, buffer.End); //advance our position in the pipe
      }

      pipeReader.Complete(); // mark the PipeReader as complete
      return bytes;
  }
}

public class Program {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<ReadlineBench>();
        }
}
