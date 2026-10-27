# Manual test checklist

Record whether the controller is local or streamed. For a streamed test, record both the host and client versions and the controller's connection to the client.

## Basic checks

1. Launch with no XInput controller: pulse/sequence controls should be disabled. No vibration should be requested.
2. Connect a controller, or start a Moonlight Desktop stream and press a controller button. Verify the selected slot using the live button indicator.
3. Test only the small motor at 25%, 50% and 100%, then only the large motor at the same levels. A low setting may be hard to feel; report that honestly.
4. Test both motors. If the large motor masks the small one, use a low large-motor setting and a high small-motor setting.
5. Check normal pulse completion, STOP and Esc. Both motors should stop.
6. Run the preset sequence. It should run the small motor first, then the large motor, with stops between effects.
7. Start another test while a pulse is active. It should replace the old request.
8. Close the app during a pulse. Check that the controller stops.
9. Disconnect during a sequence. Reconnect and confirm that the cancelled sequence does not automatically resume.
10. If multiple controllers are available, verify that effects go only to the selected slot. Selecting another controller should stop the old effect.

## A useful report

```text
Tool version:
Windows version:
Local or streamed:
Controller model / firmware / mode:
Connection (USB, dongle, Bluetooth):
Sunshine version, if streamed:
Moonlight client / version / device, if streamed:
Selected XInput slot:
Small-only result:
Large-only result:
Mixed result:
STOP / Esc / close result:
Relevant log lines:
```

Only mark checks you actually performed. API success is not a physical measurement. A successful stream test confirms the tested combination of host, client, controller, firmware and transport; it does not establish support for other combinations.
