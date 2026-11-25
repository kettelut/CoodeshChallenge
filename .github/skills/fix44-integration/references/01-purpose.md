# FIX 4.4 Integration

## Purpose

Provide specialized guidance for implementing and maintaining FIX Protocol 4.4 integrations using QuickFIX/n and C#.

Use this skill when:

- Implementing FIX Initiator or Acceptor applications.
- Configuring FIX sessions.
- Creating or processing NewOrderSingle messages.
- Generating ExecutionReports.
- Managing FIX session lifecycle.
- Correlating asynchronous responses.
- Handling retransmissions and duplicate messages.
- Implementing message validation.
- Troubleshooting FIX communication.
- Testing FIX integrations.
- Reviewing FIX protocol compliance.

The primary objective is to establish reliable communication between applications while keeping protocol-specific concerns isolated from business logic.

---

## 1. Technology Context

The Coodesh OrderProcessing challenge uses:

- C#.
- .NET 10.
- QuickFIX/n.
- FIX Protocol 4.4.

The solution contains two applications:

### OrderGenerator

Responsibilities:

- Act as FIX Initiator.
- Establish a FIX session with OrderAccumulator.
- Receive financial order requests from the web application.
- Construct NewOrderSingle messages.
- Send orders through the FIX session.
- Receive ExecutionReports.
- Correlate responses with submitted orders.
- Return processing results to the application layer.

### OrderAccumulator

Responsibilities:

- Act as FIX Acceptor.
- Accept FIX session connections.
- Receive NewOrderSingle messages.
- Validate protocol-level message structure.
- Translate messages into application contracts.
- Delegate business processing to application services.
- Generate ExecutionReports.
- Send responses through the FIX session.

### Architectural Rule

FIX communication must remain an infrastructure concern.

Domain and application logic must not depend directly on QuickFIX/n message types, session objects or protocol-specific callbacks.

---

## 2. FIX Protocol Fundamentals

FIX is a message-based financial communication protocol.

A FIX message consists of fields identified by numeric tags and values separated by the SOH delimiter.

Common message categories include:

- Session messages.
- Administrative messages.
- Application messages.

The implementation must distinguish protocol-level responsibilities from business-level order processing.

### FIX Version

The required protocol version is:

`FIX.4.4`

Configure the session consistently on both Initiator and Acceptor.

Do not mix FIX versions or assume that fields available in newer FIX versions are valid in FIX 4.4.

### Message Responsibilities

| Message | Purpose |
|---|---|
| Logon (A) | Establish a FIX session |
| Logout (5) | Terminate a FIX session |
| Heartbeat (0) | Maintain session liveness |
| TestRequest (1) | Verify session responsiveness |
| ResendRequest (2) | Request message retransmission |
| SequenceReset (4) | Manage sequence numbers |
| Reject (3) | Report a session-level message rejection |
| BusinessMessageReject (j) | Report an application-level message rejection |
| NewOrderSingle (D) | Submit a new order |
| ExecutionReport (8) | Report order status or execution events |

Use the correct message type for each protocol or business outcome.

---

## 3. QuickFIX/n Integration Architecture

Use QuickFIX/n as the protocol adapter.

Recommended logical components:

- FIX session configuration.
- Initiator or Acceptor bootstrap.
- Application callback implementation.
- Message translators.
- Session lifecycle management.
- Application service integration.
- ExecutionReport generation.
- Pending request correlation.

Keep these responsibilities separated.

### Initiator Responsibilities

The Initiator:

- Loads session settings.
- Creates the message store.
- Configures logging.
- Creates the FIX application.
- Creates the Initiator.
- Starts and stops the Initiator.
- Sends application messages after the session is available.

### Acceptor Responsibilities

The Acceptor:

- Loads session settings.
- Creates the message store.
- Configures logging.
- Creates the FIX application.
- Creates the message acceptor.
- Creates the Acceptor.
- Accepts incoming connections.
- Processes application messages.
- Stops the Acceptor during application shutdown.

### Application Boundary

The FIX application implementation should translate messages and delegate business processing.

Do not implement financial calculations directly inside:

- `FromApp`.
- `ToApp`.
- Session callbacks.
- Message handlers.
- QuickFIX/n application classes.

---

## 4. FIX Session Configuration

Configure the Initiator and Acceptor with matching session parameters.

A typical session configuration includes:

| Setting | Purpose |
|---|---|
| ConnectionType | Initiator or Acceptor |
| BeginString | FIX.4.4 |
| SenderCompID | Local application identifier |
| TargetCompID | Remote application identifier |
| SocketConnectHost | Acceptor host for Initiator |
| SocketConnectPort | Acceptor port for Initiator |
| SocketAcceptPort | Listening port for Acceptor |
| HeartBtInt | Heartbeat interval |
| StartTime | Session start time |
| EndTime | Session end time |
| UseDataDictionary | Enable FIX dictionary validation |
| DataDictionary | FIX 4.4 dictionary path |
| FileStorePath | Message persistence directory |
| FileLogPath | Session log directory |
| ResetOnLogon | Sequence reset behavior |
| ResetOnLogout | Sequence reset behavior |
| ResetOnDisconnect | Sequence reset behavior |

Use only settings supported by the installed QuickFIX/n version.

### Session Identity

The session identity is defined by:

- BeginString.
- SenderCompID.
- TargetCompID.

The Initiator's SenderCompID must correspond to the Acceptor's TargetCompID.

The Initiator's TargetCompID must correspond to the Acceptor's SenderCompID.

### Configuration Rules

- Keep environment-specific values outside source code.
- Validate mandatory settings during startup.
- Use separate configuration for development and production.
- Avoid committing sensitive credentials.
- Use predictable file store and log paths.
- Document required configuration values.
- Ensure both applications agree on session identifiers.

### Sequence Numbers

Do not enable sequence resets indiscriminately.

Understand the consequences of:

- `ResetOnLogon`.
- `ResetOnLogout`.
- `ResetOnDisconnect`.

Resetting sequence numbers can affect recovery, duplicate detection and message retransmission.

Choose a strategy consistent with the intended session persistence model.

---

