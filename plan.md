# V2X Dashboard Server Implementation Plan

## 1) Server Analysis Summary (Current State)

### Strengths
- Clear service and repository split is already in place.
- Minimal API endpoint groups are available and map to focused services.
- Core V2X message families are supported end to end: CAM, DENM, MAPEM, SPATEM, SREM, SSEM.
- Correlation logic (SREM <-> SSEM) exists with strict and fallback matching.
- Paging exists on key query endpoints.

### Gaps Against Target Requirements
- No true cyclic ingestion mode (scheduled background processing).
- No cumulative safety controls (idempotent processing and duplicate prevention by file fingerprint).
- Secure communication support is not implemented in parser/database/API; UI currently has placeholders only.
- OBU/RSU and station capability classification is distributed across query logic and UI, not centralized as domain model.
- Some read models are still weakly normalized for long-term analytics and extensibility.

### Architecture Risks
- Ingestion orchestration still has too many responsibilities in one service.
- Process-all endpoint is manually triggered only; no operations policy for periodic runs.
- Potential operational ambiguity around reprocessing behavior and data retention.

## 2) Engineering Principles For Next Iteration

- Interface-first: define contracts before implementation.
- Minimal API first for new server features.
- Single responsibility: ingestion, parsing, classification, and analytics projections separated.
- Idempotency by design for all ingestion commands.
- Observable by default: structured logs, counters, and health checks.
- Backward compatibility where possible for existing client routes.

## 3) Target Capability Map

### A. Ingestion Modes
- Cumulative mode: append-only ingest with duplicate file detection.
- Cyclic mode: scheduled processing loop with configurable interval and file selection policy.

### B. Classification and Profiling
- Station profile projection:
  - entity type (OBU / RSU / EVENT)
  - station type
  - vehicle role/category
  - observed message families
  - secure communication support flags

### C. Security Visibility
- Parse and persist security metadata when present:
  - is secured
  - signature present
  - signature valid (if inferable)
  - signer/certificate id (if available)
  - encryption indication

### D. API and Query Layer
- Stable, paged, filterable minimal APIs for:
  - packets
  - messages
  - map entities
  - correlations
  - station profiles
  - ingestion jobs and scheduler status

## 4) Interface-First Design (Proposed Contracts)

### Ingestion
- IIngestionCoordinator
  - ProcessFileAsync
  - ProcessBatchAsync
- IIngestionScheduler
  - StartAsync
  - StopAsync
  - GetStatusAsync
- IIngestionDeduplicationService
  - IsAlreadyProcessedAsync
  - MarkProcessedAsync

### Parsing / Classification
- ISecurityMetadataExtractor
- IStationProfileProjector
- IMessageCapabilityTracker

### Persistence
- IProcessedFileRepository
- IStationProfileRepository
- ISecurityObservationRepository

### Read Models
- IStationProfileQueryService
- IIngestionStatusQueryService

## 5) Database Evolution Plan

### Migration 1: Processed files (idempotency)
- New table: processed_files
  - file_name
  - file_size
  - file_hash
  - first_seen_at
  - processed_at
  - status
  - error
- Unique key on file_hash (or file_name + file_size + last_write_time strategy).

### Migration 2: Packet/message security metadata
- Add security columns to packets and/or per-message tables.
- Add index support for secure filters.

### Migration 3: Station profile projection
- New table: station_profiles
  - station_id
  - entity_type
  - station_type
  - vehicle_category
  - supports_secure_comm
  - observed_message_types (normalized relation preferred)
  - first_seen_at / last_seen_at

## 6) Minimal API Roadmap

### New endpoint groups
- /api/ingestion
  - process file
  - process all
  - scheduler status
  - scheduler control (start/stop)
- /api/stations
  - profiles
  - profile details
  - capabilities
- /api/security
  - secure message metrics
  - secure stations summary

### Existing endpoint compatibility
- Keep current routes during transition.
- Add deprecation markers and migration notes for consumers.

## 7) Implementation Phases

### Phase 1: Idempotent cumulative mode
- Implement processed file repository and deduplication checks.
- Integrate checks into ingestion flow.
- Add integration tests for duplicate processing behavior.

### Phase 2: Cyclic mode
- Add background scheduler service with interval from configuration.
- Add scheduler status and control APIs.
- Add health checks and operational logging.

### Phase 3: Security metadata support
- Extend parser for security fields where available.
- Persist metadata and expose query endpoints.
- Surface secure metrics in dashboard data contracts.

### Phase 4: Station profile domain
- Build station profile projection pipeline.
- Add station profile minimal APIs with paging/filtering.
- Align map/entity filtering to profile-backed data.

### Phase 5: Hardening and cleanup
- Move residual mixed responsibilities out of orchestration service.
- Add validation and consistent ProblemDetails responses.
- Expand integration tests and add performance smoke tests.

## 8) Testing Strategy

- Integration tests for ingestion mode behavior:
  - new file processed
  - duplicate file skipped
  - cyclic run processes only new arrivals
- Contract tests for new minimal APIs.
- Parser tests for security metadata extraction paths.
- Repository tests for paging, filtering, and indices usage.

## 9) Operational and Config Plan

- Add config section: Ingestion
  - Mode: Manual | Cumulative | Cyclic
  - IntervalSeconds
  - MaxFilesPerRun
  - ReprocessPolicy
- Add metrics/logging:
  - files scanned
  - files processed
  - files skipped (duplicate)
  - processing duration
  - secure messages detected

## 10) Acceptance Criteria

- Cumulative mode is idempotent and safe to rerun.
- Cyclic mode runs automatically with configurable interval.
- Secure communication support is queryable via API and visible in summaries.
- OBU/RSU and vehicle/station categorization is centralized and consistent.
- New features delivered through focused interfaces and minimal APIs.
- Existing API consumers remain functional during migration.
