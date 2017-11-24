using clearpixels.Helpers.JSON;

namespace ikutku.Models.json
{
    public class ResultJSON : JsonData
    {
        public ResultJSON(string apistat = "")
        {
            quota = apistat;
        }

        public string quota { get; set; }
        public new object data { get; private set; }

        public void AddOKMessage(string msg)
        {
            message = msg;
            success = true;
            data = null;
        }

        public void AddOKData(object data)
        {
            message = "";
            success = true;
            this.data = data;
        }

        public void AddFailMessage(string msg)
        {
            message = string.Format("<span class='error'>{0}</span>", msg);
            success = false;
        }

    }
}