using System.Runtime.CompilerServices;

namespace Hexus.Daemon;

public class CircularBuffer<T> where T : class
{
    private readonly T?[] _buffer;
    private int _writeIndex;

    public CircularBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 2);

        _buffer = new T?[capacity];
    }

    public void Write(T item)
    {
        _buffer[_writeIndex] = item;
        _writeIndex = (_writeIndex + 1) % _buffer.Length;
    }

    public async IAsyncEnumerable<T> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var readIndex = (_writeIndex + _buffer.Length - 1) % _buffer.Length;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (readIndex != _writeIndex)
            {
                var bufferRead = _buffer[readIndex];

                if (bufferRead is not null)
                {
                    yield return bufferRead;
                }

                readIndex = (readIndex + 1) % _buffer.Length;
                continue;
            }

            try
            {
                // Wait a bit before checking again
                await Task.Delay(100, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Discard the exception
            }
        }
    }
}
