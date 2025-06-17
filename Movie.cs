public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string ImageUrl { get; set; }

}
public class Session
{
    public DateTime StartDatetime { get; set; }
    public int HallId { get; set; }
    public decimal Price { get; set; }
}