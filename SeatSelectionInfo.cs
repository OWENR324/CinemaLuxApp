public class SeatSelectionInfo
{
    public int SessionId { get; set; }
    public int SeatNumber { get; set; }
    public string UserEmail { get; set; }
    public string MovieTitle { get; set; }
    public int HallId { get; set; }
    public int MovieId { get; set; }
    public decimal SessionPrice { get; set; } // Изменили с TicketPrice на SessionPrice
}