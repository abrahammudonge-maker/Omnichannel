# WhatsApp Template Messages: Integration Guide

Your system decides who to message and what goes into each message. Our omnichannel platform sends it
to the customer on WhatsApp using one of your organization's Meta-approved message templates (order
updates, payment reminders, appointment notices, statements and so on).

How it works:

1. You make **one HTTPS call** with a template name and up to 100 recipients.
2. We check every recipient straight away and answer within a fraction of a second: each one is either
   **accepted** (queued for sending) or **rejected** with the reason.
3. We send the accepted messages in the background, usually within a couple of seconds. If WhatsApp is
   busy or briefly unavailable, we retry automatically for up to about 45 minutes.
4. You can check the status of any message or batch at any time (`sent`, `delivered`, `read`, `failed`).

For one-time codes (OTP), use the separate OTP guide and the `/api/otp/send` endpoint instead. This
endpoint refuses AUTHENTICATION templates.

---

## 1. What you'll receive from us

| Item | Value |
|---|---|
| Base URL (test) | `https://test.servicesuitecloud.com/omnichannel-api` |
| Base URL (production) | Sent separately when we go live |
| API key (test) | Sent separately |
| API key (production) | Sent separately when we go live |
| Template list | The approved templates you can use, with how many variables each takes |

The API key is a secret. Keep it server-side only (environment variable or secrets vault). Never put
it in a mobile app, browser JavaScript or a git repository. If it leaks, tell us and we'll revoke it
and issue a new one.

The key is tied to one of our WhatsApp business numbers. Every message you send with it comes from
that number, and you can only use templates that belong to that number. The key only works for the
endpoints below. Calls to any other endpoint are rejected with `401`.

---

## 2. Authentication

Send the key in a header on every request:

```
X-Api-Key: <your key>
Content-Type: application/json
```

Missing, wrong or revoked key → `401 Unauthorized`.

---

## 3. List the templates you can use

```
GET {BaseUrl}/api/whatsapp/templates
```

```json
{
  "success": true,
  "message": "Templates retrieved successfully.",
  "data": [
    {
      "id": "6c1f2a9e-7b3d-4f10-8e2a-5d9c0b4a7e11",
      "name": "payment_reminder",
      "language": "en",
      "category": "UTILITY",
      "bodyText": "Hello {{1}}, your payment of {{2}} is due on {{3}}.",
      "parameterCount": 3
    }
  ],
  "errors": []
}
```

Only approved templates for your key's number are listed. `parameterCount` is how many values each
recipient needs.

> **About the examples in this guide:** `payment_reminder` and its wording are an illustration only.
> Your real template names, wording and number of values are what this endpoint returns for your
> key. Use those in place of the example.

You can refer to a template by its **name** (simplest; names never change) or by its `id`. New
templates are created and submitted to Meta by us. As soon as Meta approves one, you can use it by
name. There's nothing to refresh on your side.

---

## 4. Send a template

```
POST {BaseUrl}/api/whatsapp/send-template
```

### Request body

```json
{
  "templateName": "payment_reminder",
  "recipients": [
    {
      "phoneNumber": "+254712345678",
      "bodyParameters": ["Jane", "KES 4,500", "15 Oct 2026"],
      "idempotencyKey": "invoice-88231-reminder-1",
      "customerName": "Jane Wanjiku"
    },
    {
      "phoneNumber": "0722000111",
      "bodyParameters": ["Peter", "KES 1,200", "15 Oct 2026"],
      "idempotencyKey": "invoice-88232-reminder-1"
    }
  ]
}
```

| Field | Required | Rules |
|---|---|---|
| `templateName` | One of these two | The template's name, e.g. `payment_reminder`. |
| `templateId` | One of these two | The template's `id` from the list, instead of the name. |
| `language` | Only if needed | Only needed when the same template name exists in several languages, e.g. `en`, `sw`. |
| `recipients` | Yes | 1 to 100 entries per request. |
| `recipients[].phoneNumber` | Yes | WhatsApp number in international format, e.g. `+254712345678` or `254712345678`. Spaces, dashes and a leading `+` or `00` are fine. Kenyan local formats (`0712345678`, `712345678`) are also accepted and treated as `+254`. For any other country, always include the country code. |
| `recipients[].bodyParameters` | Yes, if the template has variables | Values for `{{1}}`, `{{2}}`, … in order. The count must equal the template's `parameterCount`. Every value must be a non-empty string with no new lines or tabs and no more than four spaces in a row. |
| `recipients[].idempotencyKey` | **Strongly recommended** | Your own unique ID for this one message, up to 100 characters (e.g. your invoice or notification ID). If you send the same key again, at any time, we return the original message instead of sending a second one. This is what makes retries safe. |
| `recipients[].customerName` | No | Used as the contact name in our inbox the first time we see this number. |

### Response (HTTP 202)

Each recipient gets its own result, in the same order you sent them:

```json
{
  "success": true,
  "message": "2 message(s) queued for delivery.",
  "data": {
    "batchId": "d4959802-6a83-4e8d-9422-96f1cf53bc38",
    "templateId": "6c1f2a9e-7b3d-4f10-8e2a-5d9c0b4a7e11",
    "templateName": "payment_reminder",
    "accepted": 2,
    "rejected": 0,
    "results": [
      {
        "sendId": "714c8b86-27af-4d25-a4cd-324417f7810b",
        "phoneNumber": "254712345678",
        "accepted": true,
        "status": "queued",
        "idempotencyKey": "invoice-88231-reminder-1",
        "replayed": false,
        "error": null
      },
      {
        "sendId": "0b5e1f7a-93c2-4d8e-a6f1-2c7d9e4b8a10",
        "phoneNumber": "254722000111",
        "accepted": true,
        "status": "queued",
        "idempotencyKey": "invoice-88232-reminder-1",
        "replayed": false,
        "error": null
      }
    ]
  },
  "errors": []
}
```

If a recipient fails the checks, only that recipient is rejected and the others are still queued.
For example, if Peter had been sent only two values, his result would be:

```json
{
  "sendId": null,
  "phoneNumber": "0722000111",
  "accepted": false,
  "status": "rejected",
  "idempotencyKey": "invoice-88232-reminder-1",
  "replayed": false,
  "error": "This template needs 3 body parameter(s), got 2."
}
```

- `accepted: true` means we've stored the message and will send it. Keep the `sendId` if you want to
  check on it later. You can also look it up by your `idempotencyKey`.
- `accepted: false` means nothing was stored or sent for that recipient. `error` says why. Fix it
  and send that recipient again.
- `replayed: true` means we already had a message with this `idempotencyKey`. Nothing new was sent,
  and `sendId` and `status` are those of the original message.
- `phoneNumber` on accepted results is the number as we normalised it (digits only, with country code).

Rejection reasons for a single recipient:

| `error` | Meaning |
|---|---|
| `Enter a valid WhatsApp number with its country code.` | The number couldn't be understood. |
| `This template needs N body parameter(s), got M.` | Wrong number of `bodyParameters`. |
| `Body parameter N is empty.` / `...can't contain new lines or tabs.` / `...more than four spaces in a row.` | WhatsApp would reject this value. |
| `idempotencyKey can be at most 100 characters.` | Shorten the key. |

---

## 5. Check delivery status (optional)

You don't have to check anything: we deliver and retry for you. Use these endpoints when you want to
show delivery status, report on a campaign, or decide whether to fall back to SMS/email.

```
GET {BaseUrl}/api/whatsapp/messages/{sendId}
GET {BaseUrl}/api/whatsapp/messages?idempotencyKey={yourKey}
GET {BaseUrl}/api/whatsapp/batches/{batchId}
```

One message:

```json
{
  "success": true,
  "message": "Message status retrieved.",
  "data": {
    "sendId": "714c8b86-27af-4d25-a4cd-324417f7810b",
    "batchId": "d4959802-6a83-4e8d-9422-96f1cf53bc38",
    "phoneNumber": "254712345678",
    "templateId": "6c1f2a9e-7b3d-4f10-8e2a-5d9c0b4a7e11",
    "status": "delivered",
    "final": true,
    "attempts": 1,
    "error": null,
    "idempotencyKey": "invoice-88231-reminder-1",
    "externalMessageId": "wamid.HBgM...",
    "conversationId": "a1b2c3d4-...",
    "createdAt": "2026-10-09T10:10:00+00:00",
    "updatedAt": "2026-10-09T10:10:04+00:00"
  }
}
```

A batch (everything accepted in one `send-template` call) returns `total`, `finished` (true once nothing
is still `queued` or `sending`), `counts` per status (e.g. `{ "delivered": 97, "failed": 3 }`) and
`messages`, the same objects as above.

### Statuses

| `status` | Meaning |
|---|---|
| `queued` | Waiting to be sent, or waiting to retry after a temporary WhatsApp problem (`error` shows the last problem). |
| `sending` | Being sent right now. |
| `sent` | WhatsApp accepted it. |
| `delivered` | It reached the customer's phone. |
| `read` | The customer opened it (only if they have read receipts on). |
| `failed` | It won't be delivered. `error` has WhatsApp's reason, e.g. `Receiver is incapable of receiving this message (Meta error 131026)`. |

`final` is `true` for `delivered`, `read` and `failed`. A message can stay at `sent` if the phone is
off or offline, and it moves on once WhatsApp reports back. Compare statuses case-insensitively.

---

## 6. Errors for the whole request

All errors use the same shape:

```json
{ "success": false, "message": "Human-readable reason", "data": null, "errors": [] }
```

| HTTP | `message` | What to do |
|---|---|---|
| 400 | `Add at least one recipient.` | Send at least one recipient. |
| 400 | `Send at most 100 recipients per request.` | Split the batch. |
| 400 | `Set templateId or templateName.` | Say which template to send. |
| 400 | `'name' exists in several languages (en, sw). Set language.` | Add `language`. |
| 400 | `Send one-time codes through /api/otp/send, not this endpoint.` | Use the OTP endpoint for codes. |
| 400 | `This template isn't approved yet (status: ...).` | Wait for Meta's approval, or contact us. |
| 400 | `The WhatsApp number this key sends from isn't connected and active.` | Setup issue on our side. Contact us. |
| 401 | (empty) | Missing, wrong or revoked API key. |
| 403 | `This API key isn't a WhatsApp key.` | You're using the wrong key (for example your OTP key). |
| 404 | `No template named '...' for this WhatsApp number.` / `Template not found for this WhatsApp number.` | Check the name against the template list. |
| 404 | `Message not found.` / `Batch not found.` | Wrong `sendId`, `batchId` or `idempotencyKey`. |
| 429 | (may be empty) | Too many requests. Back off and retry (see limits). |
| 5xx | — | Retry with the **same** request and idempotency keys. |

---

## 7. Retries

With an `idempotencyKey` on every recipient, **retrying is always safe**. If a call times out, fails
with `5xx` or `429`, or your process crashed before saving the response, send the **exact same
request** again. Recipients we already have come back with `replayed: true` and are not sent twice.
Recipients we didn't get are queued now.

Without idempotency keys, a resent request sends the messages again.

You don't need to retry messages that are `queued` or `failed`. We already retry temporary WhatsApp
problems ourselves, and `failed` means WhatsApp gave a reason that retrying won't fix. To send a
`failed` message again after fixing the cause, use a **new** `idempotencyKey` (e.g. add `-2`).

---

## 8. Limits

- **Recipients:** at most 100 per request.
- **Sending:** at most 30 `send-template` requests per minute per API key (up to 3,000 recipients a
  minute in full batches). If you expect more volume than this, tell us before go-live.
- **Status checks:** at most 120 requests per minute per API key, counted separately from sending.
- **Delivery speed:** about 5 messages per second from each WhatsApp number. A full batch of 100 is
  delivered to WhatsApp in roughly 20 seconds.
- **Meta's limits also apply.** Our WhatsApp number has a daily limit on how many different customers
  it can start conversations with. Meta raises it as the number builds a good quality rating.
  Messages customers block or report as spam lower that rating, so only message customers who
  expect to hear from you.
- **Cost:** Meta charges per message sent from a template. Check with us about which templates count
  as marketing and which as utility.

---

## 9. Example calls

### cURL

```bash
curl -X POST "https://test.servicesuitecloud.com/omnichannel-api/api/whatsapp/send-template" \
  -H "X-Api-Key: $OMNI_WHATSAPP_KEY" \
  -H "Content-Type: application/json" \
  -d '{"templateName":"payment_reminder","recipients":[{"phoneNumber":"+254712345678","bodyParameters":["Jane","KES 4,500","15 Oct 2026"],"idempotencyKey":"invoice-88231-reminder-1"}]}'
```

### C# (.NET)

```csharp
var http = new HttpClient
{
    BaseAddress = new Uri("https://test.servicesuitecloud.com/omnichannel-api/"),
    Timeout = TimeSpan.FromSeconds(15)
};
http.DefaultRequestHeaders.Add("X-Api-Key", Environment.GetEnvironmentVariable("OMNI_WHATSAPP_KEY"));

var request = new
{
    templateName = "payment_reminder",
    recipients = new[]
    {
        new { phoneNumber = "+254712345678", bodyParameters = new[] { "Jane", "KES 4,500", "15 Oct 2026" }, idempotencyKey = $"invoice-{invoiceId}-reminder-1" }
    }
};

// Safe to retry: the idempotency keys stop duplicates.
HttpResponseMessage response = null!;
for (var attempt = 1; attempt <= 3; attempt++)
{
    try
    {
        response = await http.PostAsJsonAsync("api/whatsapp/send-template", request);
        if ((int)response.StatusCode < 500 && (int)response.StatusCode != 429) break;
    }
    catch (HttpRequestException) when (attempt < 3) { }
    catch (TaskCanceledException) when (attempt < 3) { }
    await Task.Delay(TimeSpan.FromSeconds(attempt * 5));
}

var body = await response.Content.ReadFromJsonAsync<JsonElement>();
foreach (var r in body.GetProperty("data").GetProperty("results").EnumerateArray())
{
    if (!r.GetProperty("accepted").GetBoolean())
    {
        // log r.GetProperty("phoneNumber") and r.GetProperty("error"), then fix the data
    }
}
```

### Node.js

```js
const res = await fetch("https://test.servicesuitecloud.com/omnichannel-api/api/whatsapp/send-template", {
  method: "POST",
  headers: { "X-Api-Key": process.env.OMNI_WHATSAPP_KEY, "Content-Type": "application/json" },
  body: JSON.stringify({
    templateName: "payment_reminder",
    recipients: [
      {
        phoneNumber: "+254712345678",
        bodyParameters: ["Jane", "KES 4,500", "15 Oct 2026"],
        idempotencyKey: `invoice-${invoiceId}-reminder-1`,
      },
    ],
  }),
  signal: AbortSignal.timeout(15000),
});
const body = await res.json().catch(() => null);
const rejected = res.ok ? body.data.results.filter((r) => !r.accepted) : null;
```

### Python

```python
import os, requests

res = requests.post(
    "https://test.servicesuitecloud.com/omnichannel-api/api/whatsapp/send-template",
    headers={"X-Api-Key": os.environ["OMNI_WHATSAPP_KEY"]},
    json={
        "templateName": "payment_reminder",
        "recipients": [
            {
                "phoneNumber": "+254712345678",
                "bodyParameters": ["Jane", "KES 4,500", "15 Oct 2026"],
                "idempotencyKey": f"invoice-{invoice_id}-reminder-1",
            },
        ],
    },
    timeout=15,
)
body = res.json() if res.content else {}
rejected = [r for r in body.get("data", {}).get("results", []) if not r["accepted"]] if res.ok else None
```

### Checking a batch later

```bash
curl "https://test.servicesuitecloud.com/omnichannel-api/api/whatsapp/batches/$BATCH_ID" -H "X-Api-Key: $OMNI_WHATSAPP_KEY"
```

---

## 10. What the customer sees

- The message arrives from our business WhatsApp number with the approved template's wording and
  your values filled in.
- If the customer replies, the reply reaches our support inbox in the same chat, and our agents can
  answer it there.

---

## 11. Current limitations

- Only body variables (`{{1}}`, `{{2}}`, …) can be filled. Templates with variables in the header
  (including image/document headers) or in buttons can't be sent through this endpoint yet.
- Delivery status is available by polling (section 5). We don't push status updates to your system.

---

## 12. Before go-live checklist

- [ ] API key stored in a server-side secret, not in code or the client
- [ ] Template names checked against `GET /api/whatsapp/templates` with the production key
- [ ] Numbers sent with country code
- [ ] `bodyParameters` count matches each template's `parameterCount`, all values as strings, no new lines
- [ ] A unique `idempotencyKey` on every recipient, reused when retrying
- [ ] `accepted` checked per recipient, and rejected recipients logged with their `error`
- [ ] Timeouts, `5xx` and `429` retried with the same request and back-off
- [ ] Only customers who expect your messages are messaged
- [ ] Tested end to end on the test URL with a real WhatsApp number
- [ ] The API key is never logged

Questions or a leaked key: contact us right away.
