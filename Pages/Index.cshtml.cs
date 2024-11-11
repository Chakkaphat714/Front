using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace FinalProject.Pages
{
    public class IndexModel : PageModel
    {
        private readonly string _connectionString;

        public List<EmailInfo> listEmails { get; set; } = new List<EmailInfo>();

        [BindProperty]
        public string RecipientUsername { get; set; }

        [BindProperty]
        public string Subject { get; set; }

        [BindProperty]
        public string Body { get; set; }

        public IndexModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
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

        public async Task OnGetAsync()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    if (User.Identity?.Name == null)
                    {
                        return;
                    }

                    string sql = @"
                    SELECT 
                        EmailID,
                        SenderID,
                        RecipientID,
                        Subject,
                        Body,
                        DateSent,
                        IsRead,
                        IsDeleted
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
                                var email = new EmailInfo
                                {
                                    EmailID = reader.GetInt32(reader.GetOrdinal("EmailID")),
                                    EmailSender = reader.GetString(reader.GetOrdinal("SenderID")),
                                    EmailReceiver = reader.GetString(reader.GetOrdinal("RecipientID")),
                                    EmailSubject = reader.GetString(reader.GetOrdinal("Subject")),
                                    EmailMessage = reader.GetString(reader.GetOrdinal("Body")),
                                    EmailDate = reader.GetDateTime(reader.GetOrdinal("DateSent")),
                                    EmailIsRead = reader.GetBoolean(reader.GetOrdinal("IsRead"))
                                };
                                listEmails.Add(email);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // จัดการข้อผิดพลาด
                throw;
            }
        }

        public async Task<IActionResult> OnGetReadEmailAsync(int emailId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            EmailID,
                            SenderID,
                            Subject,
                            Body,
                            DateSent,
                            IsRead
                        FROM Emails 
                        WHERE EmailID = @emailId AND IsDeleted = 0";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@emailId", emailId);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var email = new EmailInfo
                                {
                                    EmailID = reader.GetInt32(reader.GetOrdinal("EmailID")),
                                    EmailSender = reader.GetString(reader.GetOrdinal("SenderID")),
                                    EmailSubject = reader.GetString(reader.GetOrdinal("Subject")),
                                    EmailMessage = reader.GetString(reader.GetOrdinal("Body")),
                                    EmailDate = reader.GetDateTime(reader.GetOrdinal("DateSent")),
                                    EmailIsRead = reader.GetBoolean(reader.GetOrdinal("IsRead"))
                                };

                                // อัพเดตสถานะการอ่าน
                                if (!email.EmailIsRead)
                                {
                                    string updateSql = "UPDATE Emails SET IsRead = 1 WHERE EmailID = @emailId";
                                    using (SqlCommand updateCommand = new SqlCommand(updateSql, connection))
                                    {
                                        updateCommand.Parameters.AddWithValue("@emailId", emailId);
                                        await reader.CloseAsync();
                                        await updateCommand.ExecuteNonQueryAsync();
                                    }
                                }

                                return new JsonResult(email);
                            }
                        }
                    }
                }

                return new JsonResult(new { error = "ไม่พบข้อความที่ต้องการ" }) { StatusCode = 404 };
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        INSERT INTO Emails (Subject, Body, DateSent, IsRead, SenderID, RecipientID) 
                        VALUES (@subject, @body, @date, @isRead, @sender, @receiver)";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@subject", Subject);
                        command.Parameters.AddWithValue("@body", Body);
                        command.Parameters.AddWithValue("@date", DateTime.Now);
                        command.Parameters.AddWithValue("@isRead", false);
                        command.Parameters.AddWithValue("@sender", User.Identity.Name);
                        command.Parameters.AddWithValue("@receiver", RecipientUsername);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                return Page();
            }
        }
    }
}