using Escademy.Dal;
using Escademy.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;


namespace Escademy.Controllers
{
    public class HomeController : Controller
    {
        private EscademyMDB db = new EscademyMDB();
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult PrivacyPolicy()
        {
            return View();
        }
        public ActionResult TermsOfService()
        {
            return View();
        }
        [HttpGet]
        public ActionResult Coaches(string filter)
        {
            ViewBag.filterCategory = filter;
            var games = db.esc_games.ToList();
            ViewBag.DBGames = games;
            var language = db.esc_languages.ToList();
            ViewBag.DBLanguage = language;
            ViewBag.db = db;
            return View();
        }
        /// <summary>
        /// This mehtod is made by Hans
        /// It is used to collec all "coachings" with a gameId == id and Verified == 1
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult _GetCoaches(int id)
        {

            var gameCoaches = new List<GameCoaching>();
            gameCoaches = (
                from coach in db.esc_profilegames
                where coach.Verified == 1 && coach.gameId == id

                join files in db.esc_profilegamesFiles on coach.Id equals files.profilegamesId into files
                join accounts in db.esc_accounts on coach.accountId equals accounts.Id
                join games in db.esc_games on coach.gameId equals games.Id
                join price in db.esc_profilegamesPricing on coach.Id equals price.profilegamesId into prices

                select new GameCoaching
                {
                    Description = coach.Description,
                    Title = coach.Title,
                    Verified = coach.Verified,
                    AccountId = coach.accountId,
                    Id = coach.Id,
                    Price = prices.Min(c => c.Price).ToString(),
                    Abbreviation = games.Abbreviation,
                    UserFullName = accounts.FirstName,
                    TotalCoached = 0,
                    GameId = games.Id,
                    GamePicture = files.FirstOrDefault().FileName,
                    ServiceType = coach.serviceTypeId,
                    Game = games.Game,
                    UserPictureThumbnail = accounts.small_thumbnail,
                }
            ).ToList();
            ViewBag.db = db;
            ViewBag.gameCoaches = gameCoaches;
            return PartialView("_GetCoaches");
        }

        public ActionResult About()
        {
            return View();
        }
        public ActionResult Contact()
        {
            var users = new List<User>();

            using (var conn = new MySql.Data.MySqlClient.MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT FirstName, LastName, ProfilePicture, Id FROM esc_accounts WHERE Id = 1 OR Id = 2";
                    var reader = cmd.ExecuteReader();
                    while(reader.Read())
                    {

                        users.Add(new User()
                        {
                            Id = reader.GetInt32("Id"),
                            FirstName = reader.GetString("FirstName"),
                            LastName = reader.GetString("LastName"),
                            Picture = reader.GetString("ProfilePicture")
                        });
                    }
                }

                    conn.Close();
            }
            ViewBag.users = users;

                return View();
        }
        public ActionResult Chat()
        {
            if (Session["user"] != null)
            {
                return View();
            }
            else return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });
        }
    }
}