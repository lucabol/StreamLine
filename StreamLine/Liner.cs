using static System.MemoryExtensions;
using System.Text;

namespace StreamLine;

public ref struct ByteLiner
{
  private int begin = 0;
  private int end = 0;

  private readonly Stream stream;
  private Span<byte> span;
  private readonly int spanLength;

  public ByteLiner(Stream stream, Span<byte> buffer) {
    if(buffer == null || buffer.Length == 0) throw new ArgumentException("buffer cannot be null or have length of zero");
    if(stream == null) throw new ArgumentException("stream cannot be null");
    
    this.stream = stream;
    this.span = buffer;
    this.spanLength = buffer.Length;
  }

  public Span<byte> ReadLine() {
    // This is like a goto, it is needed to avoid recursion retrying the search
    // after having loaded more data.
    while (true) {
      // Use this as signal that there is no more data.
      // This bizarrely works because Span<byte> converts implicitly to byte[]?.
      if(begin == -1) return null;
      
      // Look for the char which is common in \n and \r\n.
      var sSpan = span[begin..end];
      var i = sSpan.IndexOf((byte)'\n');

      if(i != -1) {
        // If it is a \r\n then the returned span must finish one byte before.
        var es = i > 0 && sSpan[i-1] == (byte)'\r' ? i-1 : i;
        // But the begin of the span must be moved just one byte after.
        begin += i + 1;
        return sSpan[..es];
      } else {
        var len = end - begin;
        // We have not found the iterm and searched through the whole buffer.
        if(len == spanLength)
            throw new Exception($"Line larger than {spanLength} bytes.");
        // Move the current searched sub-buffer at the start of the buffer.
        sSpan.CopyTo(span);
        // And read more bytes after it.
        var rbytes = stream.Read(span[len..]);
        // If there were no more read bytes, it means it is the last sub-span to return.
        if(rbytes == 0) {
          begin = -1;
          return span[0..len];
        } else {
          // Otherwise start the whole algorithm again.
          begin = 0;
          end = len + rbytes;
        }
      }
    }
  }
}

public ref struct CharLiner {
  private ByteLiner bliner;
  private Encoding encoding;
  private Span<char> charBuffer;

  public CharLiner(Stream stream, Span<byte> byteBuffer, Span<char> charBuffer, Encoding encoding) {
    if(charBuffer.Length < byteBuffer.Length)
      throw new Exception("charBuffer lenght must be at least as long as byteBuffer to store the decoded chars.");
    this.bliner = new ByteLiner(stream, byteBuffer);
    this.encoding = encoding;
    this.charBuffer = charBuffer;
  }

  public Span<char> ReadLine() {
    var bytes = bliner.ReadLine();
    var charsDecoded = encoding.GetChars(bytes, charBuffer);
    if(charsDecoded <= 0)
      return null;
    return charBuffer[..charsDecoded];
  }
}
