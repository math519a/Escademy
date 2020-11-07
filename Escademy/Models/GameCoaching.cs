namespace Escademy.Models
{
    public class GameCoaching
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public decimal SalaryUSD { get; set; }
        public string Game { get; set; }
        public int GameId { get; set; }
        public string Price { get; set; }
        public string Abbreviation { get; set; }
        public string Picture { get; set; }
        public string GamePicture { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public int ServiceType { get; set; }

        public int Verified { get; set; }

        public CoachingFAQ[] FAQElements { get; set; }
        public int IsLoggedIn { get; set; }
        public string UserEmail { get; set; }

        public string UserFullName { get; set; }
        public string UserPictureThumbnail { get; set; }

        public int TotalCoached { get; set; }
    }
}