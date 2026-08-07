using Meezan.IServices.IJob;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Meezan.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobTestController : ControllerBase
    {
        private readonly IJobService JobService;
        private readonly IBackgroundJobClient BackgroundJobClient;
        private readonly IRecurringJobManager RecurringJobManager;
        public JobTestController(IJobService jobService, IBackgroundJobClient backgroundJobClient,
            IRecurringJobManager recurringJobManager)
        {
            JobService = jobService;
            BackgroundJobClient = backgroundJobClient;
            RecurringJobManager = recurringJobManager;
        }

        [HttpGet("fireAndForgetJob")]
        public ActionResult CreateFireAndForgetJob()
        {
            BackgroundJobClient.Enqueue(() => JobService.FireAndForgetJob());
            return Ok();
        }

        [HttpGet("delayedJob")]
        public ActionResult CreateDelayedJob()
        {
            BackgroundJobClient.Schedule(() => JobService.DelayedJob(), TimeSpan.FromSeconds(60));
            return Ok();
        }

        [HttpGet("reccuringJob")]
        public ActionResult CreateReccuringJob()
        {
            RecurringJobManager.AddOrUpdate("jobId", () => JobService.ReccuringJob(), Cron.Minutely);
            return Ok();
        }

        [HttpGet("continuationJob")]
        public ActionResult CreateContinuationJob()
        {
            string parentJobId = BackgroundJobClient.Enqueue(() => JobService.FireAndForgetJob());
            BackgroundJobClient.ContinueJobWith(parentJobId, () => JobService.ContinuationJob());
            return Ok();
        }
    }
}
