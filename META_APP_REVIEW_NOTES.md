# Meta App Review — submission notes

Use this as the source text when filling out Meta's App Review form for each
permission. Meta wants a plain description of the use case plus a screen
recording showing the exact flow — this document is the description; you
still need to record the video yourself once Embedded Signup is wired up
against a real App ID.

---

## Permissions to request

- `whatsapp_business_management`
- `whatsapp_business_messaging`
- `pages_messaging`
- `pages_show_list`
- `instagram_manage_messages`
- `instagram_basic`
- `business_management`

## Use case description (use for all of the above, adjust per permission)

> Omnichannel Command Center is a customer support platform used by
> businesses to manage conversations with their own customers in one place.
> A business signs up, then connects the messaging channels it owns (a
> WhatsApp Business number, a Facebook Page, an Instagram professional
> account, or an email inbox) using Meta's Embedded Signup / Facebook Login
> for Business flow. Once connected, the business's support agents can view
> incoming customer messages from that channel and reply — the platform
> calls the Graph API to send replies and receives inbound messages via
> webhook. Each business only ever accesses its own connected accounts; the
> platform does not access any account the business hasn't explicitly
> connected through the signup flow.

## Screen recording checklist (record this once real App ID + configs exist)

1. Log into the platform as an organization admin.
2. Go to Settings → Channels.
3. Click "Connect via Meta" for WhatsApp (or Messenger/Instagram).
4. Complete the Meta popup — log in, select the business, select the
   number/Page/Instagram account, grant permissions.
5. Show the new channel account appearing in the "Connected channels" list.
6. Send a real test message from an external phone/account to that number/
   Page/Instagram account.
7. Show it arriving as a new conversation in the platform within a few
   seconds.
8. Reply from the platform as an agent.
9. Show the reply arriving on the external device.

That end-to-end loop (connect → receive → reply → confirm delivery) is
exactly what Meta's reviewers look for — it proves the permission is used
for real messaging, not just requested speculatively.

## Business Verification (separate from App Review)

Required before permissions go from Development to Live/Advanced Access.
Submitted in Business Settings → Security Center. You'll need:
- Legal business documents (registration certificate) OR, if operating as
  a sole proprietor, government-issued ID matching the business portfolio
  name.
- A business phone number and address Meta can verify.
- The privacy policy URL: `https://test.servicesuitecloud.com/omnichannel/privacy-policy`
- The terms of service URL: `https://test.servicesuitecloud.com/omnichannel/terms-of-service`

## Before submitting — fill in the placeholders

Both policy pages (`Omnichannel-frontend/src/pages/PrivacyPolicyPage.jsx`
and `TermsOfServicePage.jsx`) have `[DATE]` and `[CONTACT EMAIL]`
placeholders — replace those with real values and redeploy before Meta
reviewers (or real users) see them.
