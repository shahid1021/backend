using Microsoft.AspNetCore.Http;

namespace StudentAPI.Models
{
    public class ProjectFileUploadRequest
    {
        public IFormFile File { get; set; }
    }
}
