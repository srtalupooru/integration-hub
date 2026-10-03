# Add components using the form or a definition

Open **Components → Add component**. The **Form** editor is selected by default.
An Editor or Admin role is required to author components.

1. Enter the component name, unique ID, type, and environment. Required fields are
   marked with an asterisk. Use a different ID for each environment.
2. Add an owner, description, and technologies if useful. Technologies are a list;
   select **Add** for each entry.
3. Add messages. Choose **publishes** for events, **sends** for commands, or
   **consumes** for incoming messages. Enter the local binding ID, shared contract
   and version, and broker channel. Use the actual message name (for example
   `InvoiceCreated`) with identical casing on both sides; dots are optional. Consumed topics and streams also require a
   subscription or consumer group. Publishing and sending select the appropriate
   message type automatically.
4. Expand **HTTP endpoints and calls** when needed. An API exposes endpoints;
   a caller references the target component ID and its endpoint ID, method, and
   version. HTTP requests do not need a consumed broker message.
5. Expand **Handlers and sagas** for optional NServiceBus processing inside the
   component. Handler and saga message references use local message binding IDs.
   Outgoing effects reference local outgoing bindings; timeout references use IDs
   from the same saga. Add correlation rules and scheduling information as needed.
6. Select **Validate & preview links**. Correct validation errors and review the
   connections and matching findings. An unresolved connection does not require
   creating the other component immediately.
7. Select **Save component**. Nothing is saved by editing, switching modes,
   converting, or previewing. Any definition edit requires another preview before
   saving.

The same form is available when editing existing components. Their ID is locked
in the form. Revision checks prevent overwriting another author's saved changes.
Leaving with unsaved changes prompts you to discard them or keep editing.

## Work with YAML or JSON

Select **Definition** to edit the source directly, or choose a JSON/YAML file
(up to 1 MB). Sample buttons provide complete starting points. Samples and file
uploads replace the current draft, so copy any work you want to keep first.

Use **Format as JSON** or **Format as YAML** to convert the draft without saving.
Select **Form** to return to structured editing. If the source has syntax or schema
errors, it stays in the definition editor with validation messages and its text
intact. Schema-valid drafts with semantic errors can open in the form for repair;
validation still blocks saving until those errors are resolved.

Simply switching modes preserves the original source. Changing a form field
generates JSON: supported values are retained, while YAML comments, original
formatting, and property ordering are not preserved. Conversion also normalizes
formatting. The form's **Generated definition** disclosure shows the current source.

Existing definitions using separate `consumes`, `publishes`, and `sends` sections
remain editable. New definitions use a single `messages` list. Removing a message
does not silently remove handler or saga references; validation identifies the
references that need updating.

The form reads the application's component schema. See the
[annotated YAML reference](component-definition-reference.yaml),
[discovery rules](component-discovery.md), and
[NServiceBus processing guide](nservicebus-processing.md) for field details.
