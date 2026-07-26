namespace FfxiTempLogCollector.Core;

public static class RecordFingerprintFactory
{
    public static string Create(TempLogRawRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        // TEMPログのローテーション先やヘッダー内インデックスが変わっても
        // 同一レコードとして扱えるよう、出所ではなくレコード本体を使う。
        return HashUtil.ComputeSha1(record.RawRecordBytes);
    }
}
