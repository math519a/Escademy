using Escademy.Models;
using Escademy.Helpers;
using System.Web.Mvc;
using MySql.Data.MySqlClient;
using System.Globalization;
using Escademy.ViewModels;
using System.Web;
using System;
using Escademy.Dal;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;

namespace Escademy.Controllers
{
    public class CoachingController : BaseController
    {
        private EscademyMDB db = new EscademyMDB();
        public ActionResult Index()
        {
            try
            {
                if (Session["user"] == null)
                    return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });
                var user = GetCurrentUser();

                var data = db.esc_profilegames.Join(db.esc_games, p => p.gameId, g => g.Id, (c, g) => new { esc_profilegames = c, esc_games = g })
                    .Where(x => x.esc_profilegames.accountId.Equals(user.Id))
                    .Select(c => new CoachingVM()
                    {
                        Id = c.esc_profilegames.Id,
                        AccountId = c.esc_profilegames.accountId,
                        GameId = c.esc_profilegames.gameId,
                        Game = c.esc_games.Game,
                        Verified = c.esc_profilegames.Verified,
                        Views = c.esc_profilegames.Views,
                        Files = db.esc_profilegamesFiles.Where(f => f.profilegamesId.Equals(c.esc_profilegames.Id)).ToList()
                    }).ToList();

                return View(data);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }


        public ActionResult Edit(int id = 0)
        {
            if (id <= 0)
                return RedirectToAction("Index");
            var cVM = new CoachingVM();
            try
            {
                var games = db.esc_games.ToList();
                var serviceTypes = new SelectList(db.esc_serviceTypes.ToList(), "Id", "Name");
                ViewBag.DBGames = games;
                ViewData["DBServiceTypes"] = serviceTypes;
                ViewBag.DBHours = PriceHourModel.GetPriceHours().ToList();

                var pGame = db.esc_profilegames.Where(x => x.Id.Equals(id)).FirstOrDefault();
                if (pGame == null)
                {
                    return View();
                }

                var faqs = db.esc_faq.Where(x => x.profilegamesId.Equals(id)).ToList();
                var pricings = db.esc_profilegamesPricing.Where(x => x.profilegamesId.Equals(id)).ToList();
                var files = db.esc_profilegamesFiles.Where(x => x.profilegamesId.Equals(id)).ToList();
                cVM = new CoachingVM()
                {
                    Title = pGame.Title,
                    Description = pGame.Description,
                    GameId = pGame.gameId,
                    AccountId = pGame.accountId,
                    ServiceTypeId = pGame.serviceTypeId
                };
                cVM.Faqs = faqs;
                cVM.Pricings = pricings;
                cVM.Files = files;

                return View(cVM);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Created By:
        /// This method is used to edit a new coaching and update faqs, files, pricings for that coaching.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Edit(CoachingVM coaching)
        {
            try
            {
                if (coaching.AccountId <= 0 && coaching.GameId <= 0)
                {
                    ViewBag.Error = "Invalid model value.";
                    return View(coaching);
                }
                var games = db.esc_games.ToList();
                var serviceTypes = new SelectList(db.esc_serviceTypes.ToList(), "Id", "Name");
                ViewBag.DBGames = games;
                ViewData["DBServiceTypes"] = serviceTypes;
                ViewBag.DBHours = PriceHourModel.GetPriceHours().ToList();
                var user = GetCurrentUser();
                var pGame = db.esc_profilegames.Where(x => x.Id.Equals(coaching.Id)).FirstOrDefault();
                if (pGame == null)
                {
                    ViewBag.Error = "No game found for your reference.";
                    return View(coaching);
                }
                pGame.serviceTypeId = coaching.ServiceTypeId;
                pGame.Title = coaching.Title;
                pGame.Description = coaching.Description;
                pGame.Verified = 0;
                pGame.UpdatedDate = DateTime.UtcNow;

                var faqs = db.esc_faq.Where(x => x.profilegamesId.Equals(coaching.Id)).ToList();
                foreach (var item in faqs)
                {
                    db.esc_faq.Remove(item);
                }
                foreach (var item in coaching.Faqs)
                {
                    var faq = new esc_faq()
                    {
                        profilegamesId = pGame.Id,
                        Description = item.Description,
                        Title = item.Title
                    };
                    if (faq.Title != null && faq.Description != null)
                        db.esc_faq.Add(faq);
                }

                var pricings = db.esc_profilegamesPricing.Where(x => x.profilegamesId.Equals(coaching.Id)).ToList();
                foreach (var item in pricings)
                {
                    db.esc_profilegamesPricing.Remove(item);
                }
                foreach (var item in coaching.Pricings)
                {
                    var price = new esc_profilegamesPricing()
                    {
                        profilegamesId = pGame.Id,
                        Hours = item.Hours,
                        Price = item.Price
                    };
                    if (price.Hours > 0 && price.Price > 0)
                        db.esc_profilegamesPricing.Add(price);
                }
                if (coaching.Files != null)
                {
                    var coachingFileIds = coaching.Files.Select(s => s.Id).ToList();
                    var files = db.esc_profilegamesFiles.Where(x => x.profilegamesId.Equals(coaching.Id)).ToList();
                    var deleteImages = files.Where(f => !coachingFileIds.Contains(f.Id) && f.FileType == 1).ToList();

                    foreach (var item in deleteImages)
                    {
                        string name = item.FileName;
                        string path = Path.Combine(Server.MapPath("~/CoachingImages"), name);
                        FileInfo file = new FileInfo(path);
                        if (file.Exists)//check file exsit or not
                        {
                            file.Delete();
                        }
                        db.esc_profilegamesFiles.Remove(item);
                    }
                }


                // Save Images if newly added.
                HttpPostedFileBase images1 = Request.Files["ImageData1"];
                HttpPostedFileBase images2 = Request.Files["ImageData2"];
                HttpPostedFileBase images3 = Request.Files["ImageData3"];
                HttpPostedFileBase videos = Request.Files["VideoData"];
                List<PathImageAndVideo> listOfNames = new List<PathImageAndVideo>();

                if (images1 != null && images1.ContentLength > 0)
                {
                    string imgName = Path.GetFileNameWithoutExtension(images1.FileName);
                    imgName = user.Id + "" + coaching.GameId + "" + Guid.NewGuid() + "_" + imgName;
                    imgName += Path.GetExtension(images1.FileName);
                    string _path = Path.Combine(Server.MapPath("~/CoachingImages"), imgName);
                    listOfNames.Add(new PathImageAndVideo() { Name = imgName, FileType = 1, FilePath = "CoachingImages" });
                    images1.SaveAs(_path);
                }
                if (images2 != null && images2.ContentLength > 0)
                {
                    string imgName = Path.GetFileNameWithoutExtension(images2.FileName);
                    imgName = user.Id + "" + coaching.GameId + "" + Guid.NewGuid() + "_" + imgName;
                    imgName += Path.GetExtension(images2.FileName);
                    string _path = Path.Combine(Server.MapPath("~/CoachingImages"), imgName);
                    listOfNames.Add(new PathImageAndVideo() { Name = imgName, FileType = 1, FilePath = "CoachingImages" });
                    images2.SaveAs(_path);
                }
                if (images3 != null && images3.ContentLength > 0)
                {
                    string imgName = Path.GetFileNameWithoutExtension(images3.FileName);
                    imgName = user.Id + "" + coaching.GameId + "" + Guid.NewGuid() + "_" + imgName;
                    //imgName = user.Id + "" + model.GameId + "" + DateTime.UtcNow.ToString("dd-MM-yyyy") + "_" + imgName;
                    imgName += Path.GetExtension(images3.FileName);
                    string _path = Path.Combine(Server.MapPath("~/CoachingImages"), imgName);
                    listOfNames.Add(new PathImageAndVideo() { Name = imgName, FileType = 1, FilePath = "CoachingImages" });
                    images3.SaveAs(_path);
                }
                if (videos.ContentLength > 0)
                {
                    string videoName = Path.GetFileNameWithoutExtension(videos.FileName);
                    //videoName = user.Id + "" + model.GameId + "" + DateTime.UtcNow.ToString("dd-MM-yyyy") + "_" + videoName;
                    videoName = user.Id + "" + coaching.GameId + "" + Guid.NewGuid() + "_" + videoName;
                    videoName += Path.GetExtension(videos.FileName);
                    string _path = Path.Combine(Server.MapPath("~/CoachingVideos"), videoName);
                    listOfNames.Add(new PathImageAndVideo() { Name = videoName, FileType = 2, FilePath = "CoachingVideos" });
                    videos.SaveAs(_path);
                }

                // Save all type of file paths into database
                foreach (var item in listOfNames)
                {
                    var file = new esc_profilegamesFiles
                    {
                        profilegamesId = pGame.Id,
                        FileName = item.Name,
                        FileType = item.FileType,
                        FilePath = item.FilePath
                    };
                    db.esc_profilegamesFiles.Add(file);
                }


                db.SaveChanges();
                Session["SuccessMessage"] = "Coaching update successfully.";
                ViewBag.Success = "Coaching update successfully.";
                //return View(coaching);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return View();
            }
        }

        /// <summary>
        /// Created By:
        /// This method is used to delete a existing coaching and delete faqs, files, pricings for that coaching.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult Delete(int id = 0)
        {
            if (id <= 0)
                return RedirectToAction("Index");
            try
            {
                var pGame = db.esc_profilegames.Where(x => x.Id.Equals(id)).FirstOrDefault();
                if (pGame == null)
                {
                    Session["ErrorMessage"] = "Coaching doesnot exist.";
                    return RedirectToAction("Index");
                }
                var files = db.esc_profilegamesFiles.Where(x => x.profilegamesId.Equals(id)).ToList();
                foreach (var item in files)
                {
                    string name = item.FileName;
                    string path = Path.Combine(Server.MapPath("~/CoachingImages"), name);
                    FileInfo file = new FileInfo(path);
                    if (file.Exists)//check file exsit or not
                    {
                        file.Delete();
                    }
                    db.esc_profilegamesFiles.Remove(item);
                }
                var pricings = db.esc_profilegamesPricing.Where(x => x.profilegamesId.Equals(id)).ToList();
                foreach (var item in pricings)
                {
                    db.esc_profilegamesPricing.Remove(item);
                }
                var faqs = db.esc_faq.Where(x => x.profilegamesId.Equals(id)).ToList();
                foreach (var item in faqs)
                {
                    db.esc_faq.Remove(item);
                }
                db.esc_profilegames.Remove(pGame);
                db.SaveChanges();
                Session["SuccessMessage"] = "Coaching deleted successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [HttpGet]
        public ActionResult Create()
        {
            if (Session["user"] == null)
                return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });
            try
            {
                var games = db.esc_games.ToList();
                var serviceTypes = new SelectList(db.esc_serviceTypes.ToList(), "Id", "Name");

                ViewBag.DBGames = games;
                ViewData["DBServiceTypes"] = serviceTypes;
                ViewBag.DBHours = PriceHourModel.GetPriceHours().ToList();
                return View();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Created By: 
        /// This method is used to create a new coaching and save faqs, files, pricings for that coaching.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Create(CoachingVM model)
        {
            try
            {
                var games = db.esc_games.ToList();
                var serviceTypes = new SelectList(db.esc_serviceTypes.ToList(), "Id", "Name");
                ViewBag.DBGames = games;
                ViewData["DBServiceTypes"] = serviceTypes;
                ViewBag.DBHours = PriceHourModel.GetPriceHours().ToList();
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var user = GetCurrentUser();

                // Commented By: 
                // Commented By: 13-June-2019
                // This method is commented because now user can add multiple coachings for single game.
                //var pGame = db.esc_profilegames.Where(x => x.gameId == model.GameId && x.accountId == user.Id).FirstOrDefault();
                //if (pGame != null)
                //{
                //    ViewBag.Error = "You are already coaching in this game! Please edit or delete that coaching and try again.";
                //    return View(model);
                //}
                var profileGame = new esc_profilegames()
                {
                    gameId = model.GameId,
                    accountId = user.Id,
                    Title = model.Title,
                    Description = model.Description,
                    serviceTypeId = model.ServiceTypeId,
                    CreatedDate = DateTime.UtcNow,
                    Verified = (model.ServiceTypeId == 2 ? 1 : 0) // instantly approve custom offers
                };
                db.esc_profilegames.Add(profileGame);
                db.SaveChanges();
                if (model.Pricings != null)
                    foreach (var item in model.Pricings)
                    {
                        var price = new esc_profilegamesPricing()
                        {
                            profilegamesId = profileGame.Id,
                            Hours = item.Hours,
                            Price = item.Price
                        };
                        if (price.Hours > 0 && price.Price > 0)
                            db.esc_profilegamesPricing.Add(price);
                    }

                if (model.Faqs != null)
                    foreach (var item in model.Faqs)
                    {
                        var faq = new esc_faq()
                        {
                            profilegamesId = profileGame.Id,
                            Description = item.Description,
                            Title = item.Title
                        };
                        if (faq.Title != null && faq.Description != null)
                            db.esc_faq.Add(faq);
                    }


                HttpPostedFileBase images1 = Request.Files["ImageData1"];
                HttpPostedFileBase images2 = Request.Files["ImageData2"];
                HttpPostedFileBase images3 = Request.Files["ImageData3"];
                HttpPostedFileBase videos = Request.Files["VideoData"];
                List<PathImageAndVideo> listOfNames = new List<PathImageAndVideo>();

                if (images1 != null && images1.ContentLength > 0)
                {
                    string imgName = Path.GetFileNameWithoutExtension(images1.FileName);
                    imgName = user.Id + "" + model.GameId + "" + Guid.NewGuid() + "_" + imgName;
                    imgName += Path.GetExtension(images1.FileName);
                    string _path = Path.Combine(Server.MapPath("~/CoachingImages"), imgName);
                    listOfNames.Add(new PathImageAndVideo() { Name = imgName, FileType = 1, FilePath = "CoachingImages" });
                    images1.SaveAs(_path);
                }
                if (images2 != null && images2.ContentLength > 0)
                {
                    string imgName = Path.GetFileNameWithoutExtension(images2.FileName);
                    imgName = user.Id + "" + model.GameId + "" + Guid.NewGuid() + "_" + imgName;
                    imgName += Path.GetExtension(images2.FileName);
                    string _path = Path.Combine(Server.MapPath("~/CoachingImages"), imgName);
                    listOfNames.Add(new PathImageAndVideo() { Name = imgName, FileType = 1, FilePath = "CoachingImages" });
                    images2.SaveAs(_path);
                }
                if (images3 != null && images3.ContentLength > 0)
                {
                    string imgName = Path.GetFileNameWithoutExtension(images3.FileName);
                    imgName = user.Id + "" + model.GameId + "" + Guid.NewGuid() + "_" + imgName;
                    //imgName = user.Id + "" + model.GameId + "" + DateTime.UtcNow.ToString("dd-MM-yyyy") + "_" + imgName;
                    imgName += Path.GetExtension(images3.FileName);
                    string _path = Path.Combine(Server.MapPath("~/CoachingImages"), imgName);
                    listOfNames.Add(new PathImageAndVideo() { Name = imgName, FileType = 1, FilePath = "CoachingImages" });
                    images3.SaveAs(_path);
                }
                if (videos.ContentLength > 0)
                {
                    string videoName = Path.GetFileNameWithoutExtension(videos.FileName);
                    //videoName = user.Id + "" + model.GameId + "" + DateTime.UtcNow.ToString("dd-MM-yyyy") + "_" + videoName;
                    videoName = user.Id + "" + model.GameId + "" + Guid.NewGuid() + "_" + videoName;
                    videoName += Path.GetExtension(videos.FileName);
                    string _path = Path.Combine(Server.MapPath("~/CoachingVideos"), videoName);
                    listOfNames.Add(new PathImageAndVideo() { Name = videoName, FileType = 2, FilePath = "CoachingVideos" });
                    videos.SaveAs(_path);
                }

                // Save all type of file paths into database
                foreach (var item in listOfNames)
                {
                    var file = new esc_profilegamesFiles
                    {
                        profilegamesId = profileGame.Id,
                        FileName = item.Name,
                        FileType = item.FileType,
                        FilePath = item.FilePath
                    };
                    db.esc_profilegamesFiles.Add(file);
                }

                // Commit all database changes.
                db.SaveChanges();
                ViewBag.Success = "Coaching have been saved successfully!";

                // Redirect user to payment page of custom offer.
                if (model.ServiceTypeId == 2)
                {
                    return RedirectToAction("New", "Order", 
                        new {
                            id = profileGame.Id,
                            GameId = profileGame.gameId,
                            AccountId = profileGame.accountId,
                            Quantity = 1
                        }
                    );
                }

                // Return view
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Message = ex.Message;
                return View(model);
            }
        }

        public ActionResult Listing()
        {
            User user = null;
            if (Session["user"] != null)
            {
                user = (User)Session["user"];
            }
            return View();
        }

        //public ActionResult Details(int aId = 0, int gId = 0)
        //{
        //    ViewBag.accountId = aId;
        //    ViewBag.gameId = gId;


        //    using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
        //    {
        //        conn.Open();
        //        using (var cmd = conn.CreateCommand())
        //        {
        //            cmd.CommandText = "SELECT p.*, a.Created_at, a.FirstName, a.Description userdesc, a.ProfilePicture, a.Country FROM esc_profilegames p JOIN esc_accounts a ON a.Id=p.accountId WHERE accountId=@accId AND gameId=@gameId";
        //            cmd.Parameters.AddWithValue("@accId", aId);
        //            cmd.Parameters.AddWithValue("@gameId", gId);

        //            var reader = cmd.ExecuteReader();
        //            if (reader.Read())
        //            {
        //                ViewBag.coach = new GameCoaching()
        //                {
        //                    AccountId = aId,
        //                    GameId = gId,
        //                    Description = reader.GetString("Description"),
        //                    Title = reader.GetString("Title"),
        //                    SalaryUSD = reader.GetDecimal("SalaryUSD")
        //                };

        //                ViewBag.user = new User()
        //                {
        //                    Country = reader.GetString("Country"),
        //                    Description = reader.GetString("userdesc"),
        //                    FirstName = reader.GetString("FirstName"),
        //                    Picture = reader.GetString("ProfilePicture")
        //                };


        //                var dt = reader.GetDateTime("Created_at");
        //                ViewBag.date = $"{dt.ToString("MMM", CultureInfo.InvariantCulture)} {dt.Year}";
        //            }
        //        }
        //        conn.Close();
        //    }

        //    return View();
        //}


        /// <summary>
        /// Created By: 
        /// Updated on: 15-June-2019
        /// This method will receive coaching id and we will get the detail from profilegame table with respect to that id.
        /// </summary>
        /// <param name="aId"></param>
        /// <param name="gId"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult Detail(int id = 0)
        {
            //if (Session["user"] == null)
            //    return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });
            if (id <= 0)
                return RedirectToAction("Index");
            var user = GetCurrentUser();
            var coaching = new CoachingVM();
            List<ReviewDetailModel> reviewList = new List<ReviewDetailModel>();

            var pgame = db.esc_profilegames.Where(x => x.Id.Equals(id)).FirstOrDefault();
            if (pgame.Views == null)
                pgame.Views = 1;
            else
                pgame.Views += 1;

            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT a.Created_at, a.FirstName, a.Description, a.ProfilePicture, a.Country FROM esc_accounts a WHERE Id=@accId";
                    cmd.Parameters.AddWithValue("@accId", pgame.accountId);

                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        ViewBag.user = new User()
                        {
                            Country = reader.GetString("Country"),
                            Description = reader.GetString("Description"),
                            FirstName = reader.GetString("FirstName"),
                            Picture = reader.GetString("ProfilePicture")
                        };


                        var dt = reader.GetDateTime("Created_at");
                        ViewBag.date = $"{dt.ToString("MMM", CultureInfo.InvariantCulture)} {dt.Year}";
                    }
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM `esc_orders` as eo inner join esc_accounts as ea on eo.payer_account_id=ea.Id WHERE receiver_id=@userId and review_stars!=0";
                    cmd.Parameters.AddWithValue("@userId", pgame.accountId);
                    var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        reviewList.Add(new ReviewDetailModel()
                        {
                            ReviewComments = rdr.GetString("review_comments"),
                            ReviewStars = rdr.GetInt32("review_stars"),
                            ReviewDate = rdr.GetDateTime("CompletedDate"),
                            Reviewer = rdr.GetString("FirstName") + " " + rdr.GetString("LastName"),
                            ProfilePicture = rdr.GetString("ProfilePicture")
                        });
                    }
                    var reviewListCount = reviewList.Count();
                    float total = reviewList.Sum(x => Convert.ToInt32(x.ReviewStars));
                    var countList = reviewList.Count;

                    float avgStar;
                    avgStar = total / countList;
                    ViewBag.avgStarVal = avgStar.RoundValue();
                    ViewBag.avgCountVal = countList;
                    ViewBag.avgStarStr = avgStar.RoundString();
                }
                conn.Close();
            }

            db.SaveChanges();

            coaching.AccountId = pgame.accountId;
            coaching.GameId = pgame.gameId;
            coaching.Id = pgame.Id;
            coaching.Title = pgame.Title;
            coaching.Description = pgame.Description;

            // We can shift this to lambda query and make one hit for fetching pgame with child data.
            coaching.Files = db.esc_profilegamesFiles.Where(x => x.profilegamesId.Equals(pgame.Id)).ToList();
            coaching.Pricings = db.esc_profilegamesPricing.Where(x => x.profilegamesId.Equals(pgame.Id)).ToList();
            coaching.Faqs = db.esc_faq.Where(x => x.profilegamesId.Equals(pgame.Id)).ToList();
            return View(coaching);
        }

        /// <summary>
        /// Created By : 
        /// This method is used to check if the selected game while creating or editing the coaching is already being coached
        /// by the user or not.
        /// </summary>
        /// <param name="_gameId"></param>
        /// <returns></returns>
        [HttpGet]
        public JsonResult ExistGameInUserGamesByGameIdAndUserId(int _gameId)
        {
            var user = GetCurrentUser();
            var isExist = false;
            var _existUserProileGame = db.esc_profilegames.Where(x => x.accountId == user.Id && x.gameId == _gameId).FirstOrDefault();
            isExist = _existUserProileGame == null ? false : true;
            return Json(isExist, JsonRequestBehavior.AllowGet);
        }

        public class PathImageAndVideo
        {
            public string Name { get; set; }

            public int FileType { get; set; }

            public string FilePath { get; set; }
        }
        public ActionResult Dashboard()
        {
            User user = null;
            if (Session["user"] != null)
            {
                user = (User)Session["user"];
                var ActiveOrderList = new List<OrderDetailVM>();
                var SellerDetail = new List<User>();
                decimal TotalAmount = 0;
                int TotalActiveOrders = 0;

                using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
                {
                    conn.Open();
                    #region Get Active Orders-List
                    using (var cmdActiveOrder = conn.CreateCommand())
                    {
                        cmdActiveOrder.CommandText = "select g.Picture,concat(a1.FirstName,' ', a1.LastName) as BuyerName,o.mc_gross,o.date as OrderedDate from esc_orders o inner join esc_games g on o.game_id=g.Id inner join esc_accounts a1 on o.payer_account_id=a1.Id where o.receiver_id=@uid and o.order_status='Active'";
                        cmdActiveOrder.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdActiveOrder.ExecuteReader();
                        while (reader.Read())
                        {
                            TotalActiveOrders++;
                            TotalAmount = TotalAmount + reader.GetDecimal("mc_gross");
                            ActiveOrderList.Add(new OrderDetailVM()
                            {
                                GamePicture = reader.GetString("Picture"),
                                BuyerName = reader.GetString("BuyerName"),
                                FirstLetter_BuyerName = reader.GetString("BuyerName").Substring(0, 1),
                                Price = reader.GetDecimal("mc_gross"),
                                OrderDate = String.Format("{0:ddd, MMM d, yyyy}", @reader.GetDateTime("OrderedDate"))
                            });
                        }
                        ViewBag.ActiveOrder = ActiveOrderList;
                        ViewBag.TotalAmount = TotalAmount;
                        ViewBag.TotalActiveOrders = TotalActiveOrders;
                    }
                    #endregion

                    #region Get Seller-Detail
                    using (var cmdSeller = conn.CreateCommand())
                    {
                        cmdSeller.CommandText = "select a2.FirstName as CoachFirstName,a2.LastName as CoachLastName,a2.ProfilePicture as CoachProfilePicture,a2.Level as CoachLevel from esc_accounts a2 where a2.Id=@uid";
                        cmdSeller.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdSeller.ExecuteReader();
                        while (reader.Read())
                        {
                            ViewBag.Seller = new User()
                            {
                                FirstName = reader.GetString("CoachFirstName"),
                                LastName = reader.GetString("CoachLastName"),
                                Level = reader.GetInt32("CoachLevel"),
                                Picture = "data:image/png;base64," + reader.GetString("CoachProfilePicture")
                            };
                        }

                    }
                    #endregion
                    conn.Close();
                }

            }
            return View();
        }
        public ActionResult ManageOrders()
        {
            User user = null;
            if (Session["user"] != null)
            {
                user = (User)Session["user"];
                var NewOrderList = new List<OrderDetailVM>();
                var ActiveOrderList = new List<OrderDetailVM>();
                var DeliveredOrderList = new List<OrderDetailVM>();
                var CompletedOrderList = new List<OrderDetailVM>();
                var CancelledOrderList = new List<OrderDetailVM>();
                int NewOrderCount = 0;
                int ActiveOrderCount = 0;
                int DeliveredOrderCount = 0;
                int CompletedOrderCount = 0;
                int CancelledOrderCount = 0;
                using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
                {
                    conn.Open();
                    #region Get New Orders List
                    using (var cmdNew = conn.CreateCommand())
                    {

                        cmdNew.CommandText = "select concat(a1.FirstName,' ', a1.LastName) as BuyerName,o.Id as OrderId,o.payer_email as BuyerEmail,o.mc_gross,o.date as OrderedDate,o.order_status from esc_orders o inner join esc_accounts a1 on o.payer_account_id=a1.Id where o.receiver_id=@uid and o.order_status='New'";
                        cmdNew.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdNew.ExecuteReader();
                        while (reader.Read())
                        {
                            NewOrderCount++;
                            NewOrderList.Add(new OrderDetailVM()
                            {
                                OrderId = reader.GetInt32("OrderId"),
                                BuyerName = reader.GetString("BuyerName"),
                                BuyerEmail = reader.GetString("BuyerEmail"),
                                FirstLetter_BuyerName = reader.GetString("BuyerName").Substring(0, 1),
                                Price = reader.GetDecimal("mc_gross"),
                                OrderDate = String.Format("{0:ddd, MMM d, yyyy}", reader.GetDateTime("OrderedDate")),
                                OrderStatus = reader.GetString("order_status")
                            });
                        }
                        ViewBag.NewOrders = NewOrderList;
                        ViewBag.NewOrdersCount = NewOrderCount;
                    }
                    #endregion

                    #region Get Active Orders List
                    using (var cmdActive = conn.CreateCommand())
                    {
                        cmdActive.CommandText = "select concat(a1.FirstName,' ', a1.LastName) as BuyerName,o.Id as OrderId,o.payer_email as BuyerEmail,o.mc_gross,o.date as OrderedDate,o.order_status from esc_orders o inner join esc_accounts a1 on o.payer_account_id=a1.Id where o.receiver_id=@uid and o.order_status='Active'";
                        cmdActive.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdActive.ExecuteReader();
                        while (reader.Read())
                        {
                            ActiveOrderCount++;
                            ActiveOrderList.Add(new OrderDetailVM()
                            {
                                OrderId = reader.GetInt32("OrderId"),
                                BuyerName = reader.GetString("BuyerName"),
                                BuyerEmail = reader.GetString("BuyerEmail"),
                                FirstLetter_BuyerName = reader.GetString("BuyerName").Substring(0, 1),
                                Price = reader.GetDecimal("mc_gross"),
                                OrderDate = String.Format("{0:ddd, MMM d, yyyy}", reader.GetDateTime("OrderedDate")),
                                OrderStatus = reader.GetString("order_status")
                            });
                        }
                        ViewBag.ActiveOrders = ActiveOrderList;
                        ViewBag.ActiveOrdersCount = ActiveOrderCount;
                    }
                    #endregion

                    #region Get Delivered Orders List
                    using (var cmdDeliver = conn.CreateCommand())
                    {
                        cmdDeliver.CommandText = "select concat(a1.FirstName,' ', a1.LastName) as BuyerName,o.payer_email as BuyerEmail,o.mc_gross,o.date as OrderedDate,o.order_status from esc_orders o inner join esc_accounts a1 on o.payer_account_id=a1.Id where o.receiver_id=@uid and o.order_status='Delivered'";
                        cmdDeliver.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdDeliver.ExecuteReader();
                        while (reader.Read())
                        {
                            DeliveredOrderCount++;
                            DeliveredOrderList.Add(new OrderDetailVM()
                            {
                                BuyerName = reader.GetString("BuyerName"),
                                BuyerEmail = reader.GetString("BuyerEmail"),
                                FirstLetter_BuyerName = reader.GetString("BuyerName").Substring(0, 1),
                                Price = reader.GetDecimal("mc_gross"),
                                OrderDate = String.Format("{0:ddd, MMM d, yyyy}", reader.GetDateTime("OrderedDate")),
                                OrderStatus = reader.GetString("order_status")
                            });
                        }
                        ViewBag.DeliveredOrders = DeliveredOrderList;
                        ViewBag.DeliveredOrdersCount = DeliveredOrderCount;
                    }
                    #endregion

                    #region Get Completed Orders List
                    using (var cmdCompleted = conn.CreateCommand())
                    {
                        cmdCompleted.CommandText = "select concat(a1.FirstName,' ', a1.LastName) as BuyerName,o.payer_email as BuyerEmail,o.mc_gross,o.date as OrderedDate,o.order_status from esc_orders o inner join esc_accounts a1 on o.payer_account_id=a1.Id where o.receiver_id=@uid and o.order_status='Completed'";
                        cmdCompleted.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdCompleted.ExecuteReader();
                        while (reader.Read())
                        {
                            CompletedOrderCount++;
                            CompletedOrderList.Add(new OrderDetailVM()
                            {
                                BuyerName = reader.GetString("BuyerName"),
                                BuyerEmail = reader.GetString("BuyerEmail"),
                                FirstLetter_BuyerName = reader.GetString("BuyerName").Substring(0, 1),
                                Price = reader.GetDecimal("mc_gross"),
                                OrderDate = String.Format("{0:ddd, MMM d, yyyy}", reader.GetDateTime("OrderedDate")),
                                OrderStatus = reader.GetString("order_status")
                            });
                        }
                        ViewBag.CompletedOrders = CompletedOrderList;
                        ViewBag.CompletedOrdersCount = CompletedOrderCount;
                    }
                    #endregion

                    #region Get Cancelled Orders List
                    using (var cmdCancelled = conn.CreateCommand())
                    {
                        cmdCancelled.CommandText = "select concat(a1.FirstName,' ', a1.LastName) as BuyerName,o.payer_email as BuyerEmail,o.mc_gross,o.date as OrderedDate,o.order_status from esc_orders o inner join esc_accounts a1 on o.payer_account_id=a1.Id where o.receiver_id=@uid and o.order_status='Cancelled'";
                        cmdCancelled.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdCancelled.ExecuteReader();
                        while (reader.Read())
                        {
                            CancelledOrderCount++;
                            CancelledOrderList.Add(new OrderDetailVM()
                            {
                                BuyerName = reader.GetString("BuyerName"),
                                BuyerEmail = reader.GetString("BuyerEmail"),
                                FirstLetter_BuyerName = reader.GetString("BuyerName").Substring(0, 1),
                                Price = reader.GetDecimal("mc_gross"),
                                OrderDate = String.Format("{0:ddd, MMM d, yyyy}", reader.GetDateTime("OrderedDate")),
                                OrderStatus = reader.GetString("order_status")
                            });
                        }
                        ViewBag.CancelledOrders = CancelledOrderList;
                        ViewBag.CancelledOrdersCount = CancelledOrderCount;
                    }
                    #endregion
                    conn.Close();
                }
            }
            return View();
        }


        public ActionResult MyOrders()
        {
            User user = null;
            if (Session["user"] != null)
            {
                user = (User)Session["user"];
                var InProcessOrderList = new List<OrderDetailVM>();
                var DeliveredOrderList = new List<OrderDetailVM>();
                var CompletedOrderList = new List<OrderDetailVM>();
                var CancelledOrderList = new List<OrderDetailVM>();
                int InProcessOrderCount = 0;
                int DeliveredOrderCount = 0;
                int CompletedOrderCount = 0;
                int CancelledOrderCount = 0;
                using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
                {
                    conn.Open();
                    #region Get In-Process-Orders List of Buyer
                    using (var cmdInProcess = conn.CreateCommand())
                    {
                        cmdInProcess.CommandText = "select concat(a1.FirstName,' ', a1.LastName) as SellerName,o.Id as OrderId,a1.Email as SellerEmail,o.mc_gross,o.date as OrderedDate,o.order_status from esc_orders o inner join esc_accounts a1 on o.receiver_id=a1.Id where o.payer_account_id=@uid and (o.order_status='New' or o.order_status='Active')";
                        cmdInProcess.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdInProcess.ExecuteReader();
                        while (reader.Read())
                        {
                            InProcessOrderCount++;
                            InProcessOrderList.Add(new OrderDetailVM()
                            {
                                OrderId = reader.GetInt32("OrderId"),
                                SellerName = reader.GetString("SellerName"),
                                SellerEmail = reader.GetString("SellerEmail"),
                                FirstLetter_SellerName = reader.GetString("SellerName").Substring(0, 1),
                                Price = reader.GetDecimal("mc_gross"),
                                OrderDate = String.Format("{0:ddd, MMM d, yyyy}", reader.GetDateTime("OrderedDate")),
                                OrderStatus = reader.GetString("order_status")
                            });
                        }
                        ViewBag.InProcessOrders = InProcessOrderList;
                        ViewBag.InProcessOrdersCount = InProcessOrderCount;
                    }
                    #endregion

                    #region Get Delivered-Orders List of Buyer
                    using (var cmdDelivered = conn.CreateCommand())
                    {
                        cmdDelivered.CommandText = "select concat(a1.FirstName,' ', a1.LastName) as SellerName,o.Id as OrderId,a1.Email as SellerEmail,o.mc_gross,o.date as OrderedDate,o.order_status from esc_orders o inner join esc_accounts a1 on o.receiver_id=a1.Id where o.payer_account_id=@uid and o.order_status='Delivered'";
                        cmdDelivered.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdDelivered.ExecuteReader();
                        while (reader.Read())
                        {
                            DeliveredOrderCount++;
                            DeliveredOrderList.Add(new OrderDetailVM()
                            {
                                OrderId = reader.GetInt32("OrderId"),
                                SellerName = reader.GetString("SellerName"),
                                SellerEmail = reader.GetString("SellerEmail"),
                                FirstLetter_SellerName = reader.GetString("SellerName").Substring(0, 1),
                                Price = reader.GetDecimal("mc_gross"),
                                OrderDate = String.Format("{0:ddd, MMM d, yyyy}", reader.GetDateTime("OrderedDate")),
                                OrderStatus = reader.GetString("order_status")
                            });
                        }
                        ViewBag.DeliveredOrders = DeliveredOrderList;
                        ViewBag.DeliveredOrdersCount = DeliveredOrderCount;
                    }
                    #endregion

                    #region Get Completed-Orders List of Buyer
                    using (var cmdCompleted = conn.CreateCommand())
                    {
                        cmdCompleted.CommandText = "select concat(a1.FirstName,' ', a1.LastName) as SellerName,o.Id as OrderId,a1.Email as SellerEmail,o.mc_gross,o.date as OrderedDate,o.order_status from esc_orders o inner join esc_accounts a1 on o.receiver_id=a1.Id where o.payer_account_id=@uid and o.order_status='Completed'";
                        cmdCompleted.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdCompleted.ExecuteReader();
                        while (reader.Read())
                        {
                            CompletedOrderCount++;
                            CompletedOrderList.Add(new OrderDetailVM()
                            {
                                OrderId = reader.GetInt32("OrderId"),
                                SellerName = reader.GetString("SellerName"),
                                SellerEmail = reader.GetString("SellerEmail"),
                                FirstLetter_SellerName = reader.GetString("SellerName").Substring(0, 1),
                                Price = reader.GetDecimal("mc_gross"),
                                OrderDate = String.Format("{0:ddd, MMM d, yyyy}", reader.GetDateTime("OrderedDate")),
                                OrderStatus = reader.GetString("order_status")
                            });
                        }
                        ViewBag.CompletedOrders = CompletedOrderList;
                        ViewBag.CompletedOrdersCount = CompletedOrderCount;
                    }
                    #endregion

                    #region Get Cancelled-Orders List of Buyer
                    using (var cmdCancelled = conn.CreateCommand())
                    {
                        cmdCancelled.CommandText = "select concat(a1.FirstName,' ', a1.LastName) as SellerName,o.Id as OrderId,a1.Email as SellerEmail,o.mc_gross,o.date as OrderedDate,o.order_status from esc_orders o inner join esc_accounts a1 on o.receiver_id=a1.Id where o.payer_account_id=@uid and o.order_status='Cancelled'";
                        cmdCancelled.Parameters.AddWithValue("@uid", user.Id);
                        var reader = cmdCancelled.ExecuteReader();
                        while (reader.Read())
                        {
                            CancelledOrderCount++;
                            CancelledOrderList.Add(new OrderDetailVM()
                            {
                                OrderId = reader.GetInt32("OrderId"),
                                SellerName = reader.GetString("SellerName"),
                                SellerEmail = reader.GetString("SellerEmail"),
                                FirstLetter_SellerName = reader.GetString("SellerName").Substring(0, 1),
                                Price = reader.GetDecimal("mc_gross"),
                                OrderDate = String.Format("{0:ddd, MMM d, yyyy}", reader.GetDateTime("OrderedDate")),
                                OrderStatus = reader.GetString("order_status")
                            });
                        }
                        ViewBag.CancelledOrders = CancelledOrderList;
                        ViewBag.CancelledOrdersCount = CancelledOrderCount;
                    }
                    #endregion
                    conn.Close();
                }
            }
            return View();
        }
        // new june 12-06-2019
        public ActionResult Register()
        {
            ViewBag.success = true;
            return View();
        }

        [HttpPost]
        public ActionResult Register(User user)
        {
            ViewBag.success = false;

            // ignored for now ..
            /*
            if (!VerifyCapatcha(ConnectionString.Get("capatcha"), Request["g-recaptcha-response"]))
                return View();
            */

            // Verify input
            if (string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.Password) || string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName))
                return View();

            bool verified = false;

            // INSERT INTO esc_accounts (Email, Password, FirstName, Level) VALUES (@Email, @Password, @FirstName, 1)
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO esc_accounts (Email, Password, Level, FirstName, LastName, verified, Created_at, Country) VALUES (@Email, @Password, 2, @FirstName, @LastName, 0, @CreationDate, @Country)";

                    cmd.Parameters.AddWithValue("@Email", user.Email);
                    cmd.Parameters.AddWithValue("@Password", user.Password.ToSHA512());
                    cmd.Parameters.AddWithValue("@FirstName", user.FirstName);
                    cmd.Parameters.AddWithValue("@LastName", user.LastName);
                    cmd.Parameters.AddWithValue("@CreationDate", DateTime.UtcNow);
                    cmd.Parameters.AddWithValue("@Country", user.Country);

                    try
                    {
                        if (cmd.ExecuteNonQuery() >= 1)
                        {
                            verified = true;
                        }
                    }
                    catch (MySqlException)
                    {
                        //duplicate entry exception..
                    }
                }

                if (verified)
                {
                    string key = KeyGenerator.GetUniqueKey(new Random().Next(15, 30));
                    var reglink = "https://www.escademy.com/auth/confirm_mail?reg=" + key;

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "INSERT INTO esc_verificationcodes(VerificationCode, Email) VALUES (@VerificationCode, @Email)";
                        cmd.Parameters.AddWithValue("@VerificationCode", key);
                        cmd.Parameters.AddWithValue("@Email", user.Email);
                        cmd.ExecuteNonQuery();
                    }

                    var fileContents = System.IO.File.ReadAllText(Server.MapPath(@"~/App_Data/bf_confirm_mail.html"))
                        .Replace("REP_ACTIVACTION_URL", reglink)
                        .Replace("{FirstName}", user.FirstName);


                    using (var mFactory = new MailFactory())
                    {
                        mFactory.SendMail(
                            "welcome to escademy",
                            //"<html><body><h1>tak for din registrering hos escademy.</h1>tryk på linket forneden for at færdigøre registreringen<br /><a href=\"" + reglink + "\">" + reglink + "</a></body></html>",
                            fileContents,
                            new MailAddress(user.Email)
                        );
                    }
                }

                conn.Close();

                if (verified)
                    return RedirectToAction("Success", "Auth");
                else
                    return View();
            }
        }

    }
}