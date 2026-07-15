namespace Zero72.Blog.Reading;

public interface IReadingLibraryStore
{
    IReadOnlyList<ReadingBook> GetBooks();

    ReadingBook? GetBook(Guid id);

    ReadingBook CreateBook(SaveReadingBookRequest request);

    ReadingBook? UpdateBook(Guid id, SaveReadingBookRequest request);

    bool DeleteBook(Guid id);

    IReadOnlyList<ReadingRecord> GetRecords(DateOnly? from = null, DateOnly? to = null, Guid? bookId = null);

    ReadingRecord? GetRecord(Guid id);

    ReadingRecord? CreateRecord(SaveReadingRecordRequest request);

    ReadingRecord? UpdateRecord(Guid id, SaveReadingRecordRequest request);

    bool DeleteRecord(Guid id);
}
