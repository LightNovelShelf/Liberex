namespace Liberex.Models;


public class Pagination
{
    public Pagination(int page, int total, int tatalPages)
    {
        Page = page;
        Total = total;
        TatalPages = tatalPages;
    }

    public int Page { get; set; }
    public int Total { get; set; }
    public int TatalPages { get; set; }
}