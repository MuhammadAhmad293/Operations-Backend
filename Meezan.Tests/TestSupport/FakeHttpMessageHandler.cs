using System.Net;
using System.Text;

namespace Meezan.Tests.TestSupport
{
    // A scripted HttpMessageHandler: each call to SendAsync dequeues and invokes the next queued
    // responder, so a test can line up an exact sequence of responses/failures (e.g. "fail twice
    // then succeed" for retry tests, or "always fail" for circuit-breaker tests) with zero real
    // network I/O. RequestCount lets a test assert exactly how many attempts actually happened.
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();
        private Func<HttpRequestMessage, HttpResponseMessage>? _defaultResponder;

        public int RequestCount { get; private set; }

        public FakeHttpMessageHandler Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responders.Enqueue(responder);
            return this;
        }

        public FakeHttpMessageHandler EnqueueJson(HttpStatusCode statusCode, string json)
            => Enqueue(_ => new HttpResponseMessage(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

        public FakeHttpMessageHandler EnqueueFailure(HttpStatusCode statusCode = HttpStatusCode.ServiceUnavailable)
            => Enqueue(_ => new HttpResponseMessage(statusCode));

        // Used once the queue is empty — for endpoints a test calls repeatedly (e.g. SyncAsync
        // called several times in a loop) where every call should get the same canned response,
        // rather than pre-enqueuing N identical copies.
        public FakeHttpMessageHandler AlwaysRespondJson(HttpStatusCode statusCode, string json)
        {
            _defaultResponder = _ => new HttpResponseMessage(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_responders.Count > 0)
                return Task.FromResult(_responders.Dequeue()(request));
            if (_defaultResponder is not null)
                return Task.FromResult(_defaultResponder(request));
            throw new InvalidOperationException("FakeHttpMessageHandler has no more queued responses and no default responder set.");
        }
    }
}
