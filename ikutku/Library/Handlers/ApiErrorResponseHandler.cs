using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace ikutku.Library.Handlers
{
    public class ApiErrorResponseHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return base.SendAsync(request, cancellationToken).ContinueWith((responseToCompleteTask) =>
            {
                HttpResponseMessage response = responseToCompleteTask.Result;

                HttpError error = null;
                if (response.TryGetContentValue<HttpError>(out error))
                {
                    error.Message = "Oops! Unexpected error";
                }

                return response;
            });
        }
    }
}