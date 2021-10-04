# StreamLine
Reads lines of ascii or endoded text from a stream without allocating memory and faster than `StreamReader.ReadLine`.

If one of the lines is larger than the buffer, an exception is thrown. Such is life in no-allocation land.

I have no optimized the code or tested it beyond the testcases.

## Example
Read Ascii lines with:

```csharp
    Span<byte> buffer = stackalloc byte[BufferLength];
    ByteLiner l = new(memoryStream, buffer);
    Span<byte> span = stackalloc byte[0]; // https://github.com/dotnet/roslyn/issues/53014

    int sum = 0;
    while((span = l.ReadLine()) != null) {
      sum += span.Length;
    }
```

Read characters with whatever encoding with:

```csharp
    Span<byte> buffer = stackalloc byte[BufferLength];
    Span<char> outBuffer = stackalloc char[BufferLength];

    CharLiner l = new(memoryStream, buffer, outBuffer, utf8);

    int sum = 0;
    Span<char> chars = stackalloc char[0];
    while((chars = l.ReadLine()) != null) {
      sum += chars.Length;
    }
```

## Performance results
See [here](./StreamLine.Program/ReadlineBench-report-github.md).
