using Escademy.Helpers;
using Escademy.Models;
using MySql.Data.MySqlClient;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace Escademy.Controllers
{
    public class AuthController : Controller
    {
        // GET: Auth
        public ActionResult Index()
        {
            return RedirectToAction("Login");
        }

        [HttpPost]
        public ActionResult Login(User user)
        {
            var queryParts = Request.UrlReferrer.Query.Split('=');
            string redirectUrl = "";
            for (int i = 0; i < queryParts.Length; i++)
            {
                if (queryParts[i].Replace("?", "").Equals("redirecturl", StringComparison.CurrentCultureIgnoreCase))
                {
                    redirectUrl = queryParts[i + 1];
                    break;
                }
            }

            bool auth = false;                 
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open(); 
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM esc_accounts WHERE Email=@email AND Password=@password LIMIT 1";
                    cmd.Parameters.AddWithValue("@email", user.Email);
                    cmd.Parameters.AddWithValue("@password", user.Password.ToSHA512());

                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        auth = true;

                        Session["user"] = new User()
                        {
                            Id = reader.GetInt32("Id"),
                            Email = user.Email,
                            Password = user.Password.ToSHA512(),
                            FirstName = reader.GetString("FirstName"),
                            LastName = reader.GetString("LastName"),
                            Level = reader.GetInt32("Level"),
                            Picture = reader.GetString("ProfilePicture"),
                            Description = reader.GetString("Description")
                        };
                    }
                }

                //using (var cmd = conn.CreateCommand())
                //{
                //    cmd.CommandText = "Update esc_accounts as ea set ea.IsLoggedIn=1 where ea.Email=@useremail and Password=@userpassword";
                //    cmd.Parameters.AddWithValue("@useremail", user.Email);
                //    cmd.Parameters.AddWithValue("@userpassword", user.Password.ToSHA512());
                //    cmd.ExecuteNonQuery();
                //}              
                conn.Close();
            }
            // Success:
            if (auth)
            {
                if (string.IsNullOrEmpty(redirectUrl))
                    return RedirectToAction(actionName: "Index", controllerName: "Home");
                
                return Redirect(HttpUtility.HtmlDecode(HttpUtility.UrlDecode(redirectUrl)));
            }
            else
            {
                ViewBag.success = false;
                return View();
            }
        }

        public ActionResult Login()
        {
            ViewBag.success = true;
            return View();
        }
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
                    cmd.CommandText = "INSERT INTO esc_accounts (Email, Password, Level, FirstName, LastName, verified, Created_at, Country) VALUES (@Email, @Password, 1, @FirstName, @LastName, 0, @CreationDate, @Country)";

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
                            "Welcome to Escademy",
                            //"<html><body><h1>Tak for din registrering hos Escademy.</h1>Tryk på linket forneden for at færdigøre registreringen<br /><a href=\"" + reglink + "\">" + reglink + "</a></body></html>",
                            fileContents,
                            new MailAddress(user.Email)
                        );
                    }
                }

                conn.Close();

                if (verified)
                    return RedirectToAction("Success");
                else
                    return View();
            }
        }
        public ActionResult confirm_mail(string reg)
        {
            ViewBag.verified = false;

            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                var email = string.Empty;

                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM esc_verificationcodes WHERE VerificationCode=@VerificationCode";
                    cmd.Parameters.AddWithValue("@VerificationCode", reg);
                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        email = reader.GetString("Email");
                    }
                }

                if (email != string.Empty)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM esc_verificationcodes WHERE VerificationCode=@VerificationCode";
                        cmd.Parameters.AddWithValue("@VerificationCode", reg);
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "UPDATE esc_accounts SET verified=1 WHERE Email=@Email";
                        cmd.Parameters.AddWithValue("@Email", email);
                        cmd.ExecuteNonQuery();
                    }

                    ViewBag.email = email;
                    ViewBag.verified = true;
                }
            }

            return View();
        }

        [HttpPost]
        public ActionResult SetPicture()
        {
            if (Session["user"] != null)
            {
                var user = (User)Session["user"];
                if (Request.Files.Count > 0)
                {
                    var file = Request.Files[0];
                    if (file != null && file.ContentLength > 0)
                    {
                        // Load Img..
                        byte[] thePictureAsBytes = new byte[file.ContentLength];
                        using (BinaryReader theReader = new BinaryReader(file.InputStream))
                        {
                            thePictureAsBytes = theReader.ReadBytes(file.ContentLength);
                        }

                        // Convert img to bitmap and back
                        thePictureAsBytes = CreateThumbnail(GetRectangularBMP(thePictureAsBytes), 150);

                        // Create thumbnail of picture
                        var thumbnail_small = CreateThumbnail(thePictureAsBytes, 25);

                        // Convert to base64 and set img.
                        string thePictureDataAsString = Convert.ToBase64String(thePictureAsBytes);
                        using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
                        {
                            conn.Open();
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "UPDATE esc_accounts SET ProfilePicture=@profilePicture, small_thumbnail=@thumbnail WHERE Email=@email";
                                cmd.Parameters.AddWithValue("@email", user.Email);
                                cmd.Parameters.AddWithValue("@profilePicture", thePictureDataAsString);
                                cmd.Parameters.AddWithValue("@thumbnail", Convert.ToBase64String(thumbnail_small));
                                cmd.ExecuteNonQuery();
                            }
                            conn.Close();
                        }

                        // Set image on local profile
                        user.Picture = thePictureDataAsString;
                    }
                }
            }

            return RedirectToAction(actionName: "Index", controllerName: "Home");
        }

        public ActionResult Forgotpass()
        {
            ViewBag.mail_sent = false;
            return View();
        }

        [HttpPost]
        public ActionResult Forgotpass(string Email)
        {
            string key = KeyGenerator.GetUniqueKey(new Random().Next(15, 30));

            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO esc_userpasswordreset(PasswordResetToken, PasswordResetExpiration, Email) VALUES (@token, @exp, @mail)";
                    cmd.Parameters.AddWithValue("@token", key);
                    cmd.Parameters.AddWithValue("@exp", DateTime.UtcNow.AddDays(5));
                    cmd.Parameters.AddWithValue("@mail", Email);

                    if (cmd.ExecuteNonQuery() >= 1)
                    {
                        // SEND MAIL TO USER
                        var reset_link = "https://www.escademy.com/auth/reset_password?token=" + key;

                        var fileContents = System.IO.File.ReadAllText(Server.MapPath(@"~/App_Data/bf_resetpassword.html"))
                            .Replace("REP_RESET_LINK", reset_link)
                            .Replace("{USERNAME}", "User");


                        using (var mFactory = new MailFactory())
                        {
                            mFactory.SendMail(
                                "Password Reset",
                                fileContents,
                                new MailAddress(Email)
                            );
                        }
                    }
                }
                conn.Close();
            }

            ViewBag.mail_sent = true;
            return View();
        }

        public ActionResult Success()
        {
            return View();
        }

        public ActionResult reset_password(string token)
        {
            ViewBag.token = token;
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM esc_userpasswordreset WHERE PasswordResetToken=@token";
                    cmd.Parameters.AddWithValue("@token", token);

                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        DateTime dateExpire = reader.GetDateTime("PasswordResetExpiration");
                        if (dateExpire > DateTime.UtcNow)
                        {
                            string mail = reader.GetString("Email");
                            ViewBag.mail = mail;
                        }
                    }
                }

                conn.Close();
            }


            return View();
        }

        [HttpPost]
        public ActionResult reset_password(string token, string password)
        {
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();

                string mail = null;
                using (var cmd = conn.CreateCommand())
                {
                    // READS CURRENT TOKEN.
                    cmd.CommandText = "SELECT * FROM esc_userpasswordreset WHERE PasswordResetToken=@token";
                    cmd.Parameters.AddWithValue("@token", token);
                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        DateTime dateExpire = reader.GetDateTime("PasswordResetExpiration");
                        if (dateExpire > DateTime.UtcNow)
                        {
                            mail = reader.GetString("Email");
                        }
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    // DELETES TOKEN.
                    cmd.CommandText = "DELETE FROM esc_userpasswordreset WHERE PasswordResetToken=@token";
                    cmd.Parameters.AddWithValue("@token", token);
                    cmd.ExecuteNonQuery();
                }

                if (mail != null)
                {
                    // IF VALID TOKEN, RESET PASSWORD.
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "UPDATE esc_accounts SET `Password` = @newPass WHERE `Email`= @mail; ";
                        cmd.Parameters.AddWithValue("@newPass", password.ToSHA512());
                        cmd.Parameters.AddWithValue("@mail", mail);

                        cmd.ExecuteNonQuery();
                    }
                }


                conn.Close();
            }

            return RedirectToAction("Login");
        }


        public ActionResult Logout()
        {
            var user = (User)Session["user"];
            if(user == null)
                return RedirectToAction(actionName: "Index", controllerName: "Home");
            ////using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            //{
            //    conn.Open();
            //    using (var cmd = conn.CreateCommand())
            //    {
            //        cmd.CommandText = "Update esc_accounts as ea set ea.IsLoggedIn=0 where ea.Email=@email";
            //        cmd.Parameters.AddWithValue("@email", user.Email);
            //        cmd.ExecuteNonQuery();
            //    }
            //    conn.Close();
            //}
            Session.Clear();
            return RedirectToAction(actionName: "Index", controllerName: "Home");
        }
        public ActionResult Checkout()
        {
            return View();
        }
        public ActionResult Applycoach()
        {
            return View();
        }


        private byte[] GetRectangularBMP(byte[] thePictureAsBytes)
        {
            Bitmap bmp;
            using (var ms = new MemoryStream(thePictureAsBytes))
            {
                bmp = new Bitmap(ms);
            }

            var newBmp = MakeSquarePhoto(bmp, Math.Min(bmp.Width, bmp.Height));

            using (var stream = new MemoryStream())
            {
                newBmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                thePictureAsBytes = stream.ToArray();
            }

            bmp.Dispose();
            newBmp.Dispose();

            return thePictureAsBytes;
        }


        public static byte[] CreateThumbnail(byte[] PassedImage, int LargestSide)
        {
            byte[] ReturnedThumbnail;

            using (MemoryStream StartMemoryStream = new MemoryStream(),
                                NewMemoryStream = new MemoryStream())
            {
                // write the string to the stream  
                StartMemoryStream.Write(PassedImage, 0, PassedImage.Length);

                // create the start Bitmap from the MemoryStream that contains the image  
                Bitmap startBitmap = new Bitmap(StartMemoryStream);

                // set thumbnail height and width proportional to the original image.  
                int newHeight;
                int newWidth;
                double HW_ratio;
                if (startBitmap.Height > startBitmap.Width)
                {
                    newHeight = LargestSide;
                    HW_ratio = (double)((double)LargestSide / (double)startBitmap.Height);
                    newWidth = (int)(HW_ratio * (double)startBitmap.Width);
                }
                else
                {
                    newWidth = LargestSide;
                    HW_ratio = (double)((double)LargestSide / (double)startBitmap.Width);
                    newHeight = (int)(HW_ratio * (double)startBitmap.Height);
                }

                // create a new Bitmap with dimensions for the thumbnail.  
                Bitmap newBitmap = new Bitmap(newWidth, newHeight);

                // Copy the image from the START Bitmap into the NEW Bitmap.  
                // This will create a thumnail size of the same image.  
                newBitmap = ResizeImage(startBitmap, newWidth, newHeight);

                // Save this image to the specified stream in the specified format.  
                newBitmap.Save(NewMemoryStream, System.Drawing.Imaging.ImageFormat.Jpeg);

                // Fill the byte[] for the thumbnail from the new MemoryStream.  
                ReturnedThumbnail = NewMemoryStream.ToArray();
            }

            // return the resized image as a string of bytes.  
            return ReturnedThumbnail;
        }
        
        private static Bitmap ResizeImage(Bitmap image, int width, int height)
        {
            Bitmap resizedImage = new Bitmap(width, height);
            using (Graphics gfx = Graphics.FromImage(resizedImage))
            {
                gfx.DrawImage(image, new Rectangle(0, 0, width, height),
                    new Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
            }
            return resizedImage;
        }


        private Bitmap MakeSquarePhoto(Bitmap bmp, int size)
        {
            Bitmap res = new Bitmap(size, size);
            Graphics g = Graphics.FromImage(res);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, size, size);
            int t = 0, l = 0;
            if (bmp.Height > bmp.Width)
                t = (bmp.Height - bmp.Width) / 2;
            else
                l = (bmp.Width - bmp.Height) / 2;
            g.DrawImage(bmp, new Rectangle(0, 0, size, size), new Rectangle(l, t, bmp.Width - l * 2, bmp.Height - t * 2), GraphicsUnit.Pixel);
            return res;
        }
        private bool VerifyCapatcha(string capatchaCode, string userResponse)
        {
            var request = (HttpWebRequest)WebRequest.Create("https://www.google.com/recaptcha/api/siteverify");

            var postData = "secret=" + capatchaCode;
            postData += "&response=" + userResponse;
            var data = Encoding.ASCII.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();
            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

            return true;
        }
    }
}