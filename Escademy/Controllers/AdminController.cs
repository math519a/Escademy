using Escademy.Dal;
using Escademy.Models;
using Escademy.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Escademy.Controllers
{
    public class AdminController : Controller
    {
        private EscademyMDB db = new EscademyMDB();
        // GET: Admin
        public ActionResult Index()
        {
            User user = null;
            if (Session["user"] != null)
            {
                user = (User)Session["user"];
            }

            if (user != null && user.HasRole(UserLevel.Admin))
            {
                return View();
            }
            else
            {
                return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) }); // no permission
            }
        }

        public ActionResult Coachings()
        {
            User user = null;
            if (Session["user"] == null)
                return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });
            user = (User)Session["user"];
            if (user == null || !user.HasRole(UserLevel.Admin))
                return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });
            try
            {
                IQueryable<CoachingVM> data = (from ep in db.esc_profilegames
                                               join e in db.esc_games on ep.gameId equals e.Id
                                               join t in db.esc_accounts on ep.accountId equals t.Id
                                               where ep.accountId.Equals(ep.accountId) && ep.gameId.Equals(e.Id)
                                               select new CoachingVM
                                               {
                                                   Id = ep.Id,
                                                   AccountId = ep.accountId,
                                                   GameId = e.Id,
                                                   Game = e.Game,
                                                   Verified = ep.Verified,
                                                   Files = db.esc_profilegamesFiles.Where(f => f.profilegamesId.Equals(ep.Id)).ToList(),
                                                   FullName = t.FirstName + " " + t.LastName
                                               });

                return View(data.ToList());
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        /// <summary>
        /// Author: Hans Krogh
        /// Fixes done so it takes Id as parameter and now it works with new DB
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="verify"></param>
        /// <returns></returns>
        public ActionResult Verify(int Id,int verify)
        {
            try
            {
                User user = null;
                if (Session["user"] == null)
                    return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });
                user = (User)Session["user"];
                if (user == null || !user.HasRole(UserLevel.Admin))
                    return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });

                var pGame = db.esc_profilegames.Where(x => (x.Id == Id)).FirstOrDefault();
                pGame.Verified = verify;
                db.SaveChanges();
                return RedirectToAction("Coachings");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}