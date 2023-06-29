using System.Text;
using System.Numerics;

public class LStream
{
    public LStream(Stream s) => _stm = s;
    private Stream _stm;
    public bool IsInput => _stm.CanRead;
    public bool IsOutput => _stm.CanWrite;
    public bool IsBidirectional => IsInput && IsOutput;
    public bool IsSeekable => _stm.CanSeek;
    public bool CanTimeout => _stm.CanTimeout;
    public LVal Length => IsSeekable ? LVal.Number(new BigInteger(_stm.Length)) : LVal.Err("Cannot get the length of a non-seekable stream");
    public LVal Position => LVal.Number(_stm.Position);
    public void Close() => _stm.Close();
    public LVal SetPosition(LVal byteOffset) {
        if (IsSeekable) 
        {
            if (!byteOffset.IsNum) return LVal.Err("Invalid parameter 'byteOffset': is not a number");
            if (byteOffset.NumVal!.CompareTo(Length.NumVal) > 0) return LVal.Err("Cannot seek past end of stream");
            var offset = (long)byteOffset.NumVal!.ToInt().num;
            return LVal.Number(_stm.Position = offset);
        }
        return LVal.Err("Cannot seek on a non-seekable stream");
    }
    public LVal SetRelativePosition(LVal relativeOffset) {
        if (IsSeekable) 
        {
            if (!relativeOffset.IsNum) return LVal.Err("Invalid parameter 'byteOffset': is not a number");
            var position = Position;
            var newPosition = relativeOffset.NumVal! + position.NumVal!;
            if (newPosition!.CompareTo(Num.Zero) < 0) return LVal.Err("Cannot seek before beginning of stream");
            if (newPosition.CompareTo(Length.NumVal) > 0) return LVal.Err("Cannot seek past end of stream");
            var offset = (long)newPosition.ToInt().num;
            return LVal.Number(_stm.Position = offset);
        }
        return LVal.Err("Cannot seek on a non-seekable stream");
    }
    public LVal Read() => IsInput ? LVal.Number(_stm.ReadByte()) : LVal.Err("Cannot read from this stream");
    public LVal Write(LVal val) {
        if (IsOutput) {
            var bytes = Encoding.UTF8.GetBytes(val.Serialize());
            _stm.Write(bytes, 0, bytes.Length);
            return LVal.NIL();
         }
         return LVal.Err("Cannot write this stream");
    }
}