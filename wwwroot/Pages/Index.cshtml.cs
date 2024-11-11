using FinalProject.Areas.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using System.Data;
//new2.2.2
namespace FinalProject.Pages
{
    public class IndexModel : PageModel
    {   

        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        public List<EmailInfo> listEmails = new List<EmailInfo>();
        public List<EmailInfo> listSentEmails = new List<EmailInfo>();
        public string CurrentView { get; set; } = "inbox"; // เพิ่มตัวแปรเก็บมุมมองปัจจุบัน

        [BindProperty]
        public string RecipientUsername { get; set; }

        [BindProperty]
        public string Subject { get; set; }

        [BindProperty]
        public string Body { get; set; }

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task OnGetAsync(string view = "inbox")
        {
            CurrentView = view;
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    if (User.Identity?.Name == null)
                    {
                        _logger.LogWarning("No user is logged in");
                        return;
                    }

                    // ดึงข้อมูลกล่องข้อความเข้า
                    if (view == "inbox")
                    {
                        string sql = @"
                    SELECT 
                        EmailID, SenderID, RecipientID, Subject, Body, 
                        DateSent, IsRead, IsDeleted
                    FROM Emails 
                    WHERE RecipientID = @username 
                      AND IsDeleted = 0
                    ORDER BY DateSent DESC";

                        using (SqlCommand command = new SqlCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("@username", User.Identity.Name);
                            using (SqlDataReader reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    listEmails.Add(CreateEmailInfoFromReader(reader));
                                }
                            }
                        }
                    }
                    // ดึงข้อมูลข้อความที่ส่งแล้ว
                    else if (view == "sent")
                    {
                        string sql = @"
                    SELECT 
                        EmailID, SenderID, RecipientID, Subject, Body, 
                        DateSent, IsRead, IsDeleted
                    FROM Emails 
                    WHERE SenderID = @username 
                      AND IsDeleted = 0
                    ORDER BY DateSent DESC";

                        using (SqlCommand command = new SqlCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("@username", User.Identity.Name);
                            using (SqlDataReader reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    listSentEmails.Add(CreateEmailInfoFromReader(reader));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving emails: {Message}", ex.Message);
                throw;
            }
        }

        private EmailInfo CreateEmailInfoFromReader(SqlDataReader reader)
        {
            return new EmailInfo
            {
                EmailID = reader.GetInt32(reader.GetOrdinal("EmailID")),
                EmailSender = reader.GetString(reader.GetOrdinal("SenderID")),
                EmailReceiver = reader.GetString(reader.GetOrdinal("RecipientID")),
                EmailSubject = reader.GetString(reader.GetOrdinal("Subject")),
                EmailMessage = reader.GetString(reader.GetOrdinal("Body")),
                EmailDate = reader.GetDateTime(reader.GetOrdinal("DateSent")),
                EmailIsRead = reader.GetBoolean(reader.GetOrdinal("IsRead"))
            };
        }

        // OnPost method to handle POST request when form is submitted
        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Insert new email into the database
                    string sql = "INSERT INTO Emails (Subject, Body, DateSent, IsRead, SenderID, RecipientID) " +
                                 "VALUES (@subject, @body, @date, @isRead, @sender, @receiver)";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@subject", Subject);
                        command.Parameters.AddWithValue("@body", Body);
                        command.Parameters.AddWithValue("@date", DateTime.Now);
                        command.Parameters.AddWithValue("@isRead", 0); // Assuming 0 means unread
                        command.Parameters.AddWithValue("@sender", User.Identity.Name);
                        command.Parameters.AddWithValue("@receiver", RecipientUsername);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                // After sending email, redirect to the same page to display updated list
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while sending the email.");
                return Page(); // Stay on the same page if there's an error
            }
        }
        public int UnreadCount { get; set; }
        public int SentCount { get; set; }

    }

    public class EmailInfo
    {
        public int EmailID { get; set; }
        public string EmailSender { get; set; }
        public string EmailReceiver { get; set; }
        public string EmailSubject { get; set; }
        public string EmailMessage { get; set; }
        public DateTime EmailDate { get; set; }
        public bool EmailIsRead { get; set; }
    }
}
