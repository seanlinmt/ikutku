using System.Web.Http;
using ikutku.DB;
using ikutku.Library.ActionFilters.API;

namespace ikutku.Controllers.API
{
    [ApiAuthorize]
    [ApiExceptionFilter]
    public class baseApiController : ApiController
    {
        protected readonly IUnitOfWork _unitOfWork;

        protected baseApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        protected baseApiController()
        {
            
        }

        protected override void Dispose(bool disposing)
        {
            _unitOfWork.Dispose();
            base.Dispose(disposing);
        }
    }
}