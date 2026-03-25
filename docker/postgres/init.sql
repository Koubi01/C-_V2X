-- Create database and user (if not already done)
-- This is handled by docker-compose.yml

-- Create tables for V2X packet analysis

CREATE TABLE IF NOT EXISTS packets (
    id SERIAL PRIMARY KEY,
    timestamp TIMESTAMP NOT NULL,
    source_mac VARCHAR(17),
    destination_mac VARCHAR(17),
    packet_type VARCHAR(50),
    length INTEGER,
    protocol VARCHAR(50),
    source_ip VARCHAR(45), -- IPv6 support
    destination_ip VARCHAR(45),
    source_port INTEGER,
    destination_port INTEGER,
    is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE,
    is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE,
    security_protocol VARCHAR(50),
    signer_id VARCHAR(128),
    certificate_id VARCHAR(128),
    payload TEXT,
    pcap_file_name VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE packets ADD COLUMN IF NOT EXISTS is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE packets ADD COLUMN IF NOT EXISTS is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE packets ADD COLUMN IF NOT EXISTS security_protocol VARCHAR(50);
ALTER TABLE packets ADD COLUMN IF NOT EXISTS signer_id VARCHAR(128);
ALTER TABLE packets ADD COLUMN IF NOT EXISTS certificate_id VARCHAR(128);

-- CAM Messages table
CREATE TABLE IF NOT EXISTS cam_messages (
    id SERIAL PRIMARY KEY,
    packet_id INTEGER REFERENCES packets(id) ON DELETE CASCADE,
    generation_time TIMESTAMP NOT NULL,
    station_id VARCHAR(50),
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    altitude DOUBLE PRECISION,
    speed DOUBLE PRECISION,
    heading DOUBLE PRECISION,
    station_type INTEGER,
    vehicle_role VARCHAR(50),
    acceleration DOUBLE PRECISION,
    curvature DOUBLE PRECISION,
    yaw_rate DOUBLE PRECISION,
    lateral_acceleration DOUBLE PRECISION,
    vertical_acceleration DOUBLE PRECISION,
    decode_status VARCHAR(20),
    vehicle_length DOUBLE PRECISION,
    vehicle_width DOUBLE PRECISION,
    is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE,
    is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE,
    security_protocol VARCHAR(50),
    signer_id VARCHAR(128),
    certificate_id VARCHAR(128),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Keep existing databases aligned when schema already exists.
ALTER TABLE cam_messages ADD COLUMN IF NOT EXISTS decode_status VARCHAR(20);
ALTER TABLE cam_messages ADD COLUMN IF NOT EXISTS is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE cam_messages ADD COLUMN IF NOT EXISTS is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE cam_messages ADD COLUMN IF NOT EXISTS security_protocol VARCHAR(50);
ALTER TABLE cam_messages ADD COLUMN IF NOT EXISTS signer_id VARCHAR(128);
ALTER TABLE cam_messages ADD COLUMN IF NOT EXISTS certificate_id VARCHAR(128);
ALTER TABLE cam_messages ALTER COLUMN lateral_acceleration TYPE DOUBLE PRECISION USING lateral_acceleration::DOUBLE PRECISION;
ALTER TABLE cam_messages ALTER COLUMN vertical_acceleration TYPE DOUBLE PRECISION USING vertical_acceleration::DOUBLE PRECISION;

-- DENM Messages table
CREATE TABLE IF NOT EXISTS denm_messages (
    id SERIAL PRIMARY KEY,
    packet_id INTEGER REFERENCES packets(id) ON DELETE CASCADE,
    generation_time TIMESTAMP NOT NULL,
    station_id VARCHAR(50),
    cause_code VARCHAR(50),
    detection_time TIMESTAMP,
    reference_time TIMESTAMP,
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    altitude DOUBLE PRECISION,
    relevance_traffic_direction INTEGER,
    validity_duration BOOLEAN,
    station_type INTEGER,
    awareness_traffic_direction INTEGER,
    original_station_type INTEGER,
    is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE,
    is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE,
    security_protocol VARCHAR(50),
    signer_id VARCHAR(128),
    certificate_id VARCHAR(128),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE denm_messages ADD COLUMN IF NOT EXISTS is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE denm_messages ADD COLUMN IF NOT EXISTS is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE denm_messages ADD COLUMN IF NOT EXISTS security_protocol VARCHAR(50);
ALTER TABLE denm_messages ADD COLUMN IF NOT EXISTS signer_id VARCHAR(128);
ALTER TABLE denm_messages ADD COLUMN IF NOT EXISTS certificate_id VARCHAR(128);

-- MAPEM Messages table
CREATE TABLE IF NOT EXISTS mapem_messages (
    id SERIAL PRIMARY KEY,
    packet_id INTEGER REFERENCES packets(id) ON DELETE CASCADE,
    generation_time TIMESTAMP NOT NULL,
    station_id VARCHAR(50),
    intersection_id INTEGER,
    intersection_name VARCHAR(255),
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    lane_count INTEGER,
    road_width DOUBLE PRECISION,
    speed_limit VARCHAR(50),
    map_version VARCHAR(50),
    publisher_id VARCHAR(50),
    is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE,
    is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE,
    security_protocol VARCHAR(50),
    signer_id VARCHAR(128),
    certificate_id VARCHAR(128),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE mapem_messages ADD COLUMN IF NOT EXISTS is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE mapem_messages ADD COLUMN IF NOT EXISTS is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE mapem_messages ADD COLUMN IF NOT EXISTS security_protocol VARCHAR(50);
ALTER TABLE mapem_messages ADD COLUMN IF NOT EXISTS signer_id VARCHAR(128);
ALTER TABLE mapem_messages ADD COLUMN IF NOT EXISTS certificate_id VARCHAR(128);

-- SPATEM Messages table
CREATE TABLE IF NOT EXISTS spatem_messages (
    id SERIAL PRIMARY KEY,
    packet_id INTEGER REFERENCES packets(id) ON DELETE CASCADE,
    generation_time TIMESTAMP NOT NULL,
    station_id VARCHAR(50),
    intersection_id INTEGER,
    intersection_name VARCHAR(255),
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    current_phase INTEGER,
    phase_state VARCHAR(20),
    connection_maneuver_assist_id INTEGER,
    phase0_signal_group INTEGER,
    phase1_signal_group INTEGER,
    phase2_signal_group INTEGER,
    phase3_signal_group INTEGER,
    phase4_signal_group INTEGER,
    phase5_signal_group INTEGER,
    phase0_event_state VARCHAR(20),
    phase1_event_state VARCHAR(20),
    phase2_event_state VARCHAR(20),
    phase3_event_state VARCHAR(20),
    phase4_event_state VARCHAR(20),
    phase5_event_state VARCHAR(20),
    phase0_connection_maneuver_assist_id0 INTEGER,
    phase0_connection_maneuver_assist_id1 INTEGER,
    phase1_connection_maneuver_assist_id0 INTEGER,
    phase1_connection_maneuver_assist_id1 INTEGER,
    phase2_connection_maneuver_assist_id0 INTEGER,
    phase2_connection_maneuver_assist_id1 INTEGER,
    phase3_connection_maneuver_assist_id0 INTEGER,
    phase3_connection_maneuver_assist_id1 INTEGER,
    phase4_connection_maneuver_assist_id0 INTEGER,
    phase4_connection_maneuver_assist_id1 INTEGER,
    phase5_connection_maneuver_assist_id0 INTEGER,
    phase5_connection_maneuver_assist_id1 INTEGER,
    publisher_id VARCHAR(50),
    is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE,
    is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE,
    security_protocol VARCHAR(50),
    signer_id VARCHAR(128),
    certificate_id VARCHAR(128),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS connection_maneuver_assist_id INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase0_signal_group INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase1_signal_group INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase2_signal_group INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase3_signal_group INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase4_signal_group INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase5_signal_group INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase0_event_state VARCHAR(20);
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase1_event_state VARCHAR(20);
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase2_event_state VARCHAR(20);
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase3_event_state VARCHAR(20);
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase4_event_state VARCHAR(20);
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase5_event_state VARCHAR(20);
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase0_connection_maneuver_assist_id0 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase0_connection_maneuver_assist_id1 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase1_connection_maneuver_assist_id0 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase1_connection_maneuver_assist_id1 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase2_connection_maneuver_assist_id0 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase2_connection_maneuver_assist_id1 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase3_connection_maneuver_assist_id0 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase3_connection_maneuver_assist_id1 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase4_connection_maneuver_assist_id0 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase4_connection_maneuver_assist_id1 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase5_connection_maneuver_assist_id0 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS phase5_connection_maneuver_assist_id1 INTEGER;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS security_protocol VARCHAR(50);
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS signer_id VARCHAR(128);
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS certificate_id VARCHAR(128);

-- SREM Messages table
CREATE TABLE IF NOT EXISTS srem_messages (
    id SERIAL PRIMARY KEY,
    packet_id INTEGER REFERENCES packets(id) ON DELETE CASCADE,
    generation_time TIMESTAMP NOT NULL,
    station_id VARCHAR(50),
    intersection_id INTEGER,
    intersection_name VARCHAR(255),
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    requested_phase INTEGER,
    vehicle_type VARCHAR(50),
    request_reason VARCHAR(255),
    request_id VARCHAR(50),
    requestor_id VARCHAR(50),   
    required_accuracy VARCHAR(50),
    in_bound_lane_id INTEGER, 
    out_bound_lane_id INTEGER, 
    heading DOUBLE PRECISION, 
    speed DOUBLE PRECISION, 
    transmission_power DOUBLE PRECISION,
    route_names TEXT, 
    transit_schedule TEXT,
    requestor_name VARCHAR(255),
    is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE,
    is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE,
    security_protocol VARCHAR(50),
    signer_id VARCHAR(128),
    certificate_id VARCHAR(128),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE srem_messages ADD COLUMN IF NOT EXISTS is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE srem_messages ADD COLUMN IF NOT EXISTS is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE srem_messages ADD COLUMN IF NOT EXISTS security_protocol VARCHAR(50);
ALTER TABLE srem_messages ADD COLUMN IF NOT EXISTS signer_id VARCHAR(128);
ALTER TABLE srem_messages ADD COLUMN IF NOT EXISTS certificate_id VARCHAR(128);

-- SSEM Messages table
CREATE TABLE IF NOT EXISTS ssem_messages (
    id SERIAL PRIMARY KEY,
    packet_id INTEGER REFERENCES packets(id) ON DELETE CASCADE,
    generation_time TIMESTAMP NOT NULL,
    station_id VARCHAR(50),
    intersection_name VARCHAR(255),
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    intersection_id INTEGER,
    status_code VARCHAR(50),
    granted_duration INTEGER,
    request_id_ref VARCHAR(50),
    responder_id VARCHAR(50),
    request_station_id_ref VARCHAR(50),
    is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE,
    is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE,
    security_protocol VARCHAR(50),
    signer_id VARCHAR(128),
    certificate_id VARCHAR(128),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE ssem_messages ADD COLUMN IF NOT EXISTS is_secure_signed BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE ssem_messages ADD COLUMN IF NOT EXISTS is_secure_encrypted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE ssem_messages ADD COLUMN IF NOT EXISTS security_protocol VARCHAR(50);
ALTER TABLE ssem_messages ADD COLUMN IF NOT EXISTS signer_id VARCHAR(128);
ALTER TABLE ssem_messages ADD COLUMN IF NOT EXISTS certificate_id VARCHAR(128);

-- Create indexes for better query performance
CREATE INDEX IF NOT EXISTS idx_packets_timestamp ON packets(timestamp);
CREATE INDEX IF NOT EXISTS idx_packets_packet_type ON packets(packet_type);
CREATE INDEX IF NOT EXISTS idx_packets_pcap_file ON packets(pcap_file_name);
CREATE INDEX IF NOT EXISTS idx_packets_secure_signed ON packets(is_secure_signed);
CREATE INDEX IF NOT EXISTS idx_packets_secure_encrypted ON packets(is_secure_encrypted);
CREATE INDEX IF NOT EXISTS idx_packets_signer_id ON packets(signer_id);

-- CAM indexes
CREATE INDEX IF NOT EXISTS idx_cam_generation_time ON cam_messages(generation_time);
CREATE INDEX IF NOT EXISTS idx_cam_station_id ON cam_messages(station_id);
CREATE INDEX IF NOT EXISTS idx_cam_packet_id ON cam_messages(packet_id);

-- DENM indexes
CREATE INDEX IF NOT EXISTS idx_denm_generation_time ON denm_messages(generation_time);
CREATE INDEX IF NOT EXISTS idx_denm_cause_code ON denm_messages(cause_code);
CREATE INDEX IF NOT EXISTS idx_denm_packet_id ON denm_messages(packet_id);

-- MAPEM indexes
CREATE INDEX IF NOT EXISTS idx_mapem_generation_time ON mapem_messages(generation_time);
CREATE INDEX IF NOT EXISTS idx_mapem_intersection ON mapem_messages(intersection_id);
CREATE INDEX IF NOT EXISTS idx_mapem_packet_id ON mapem_messages(packet_id);

-- SPATEM indexes
CREATE INDEX IF NOT EXISTS idx_spatem_generation_time ON spatem_messages(generation_time);
CREATE INDEX IF NOT EXISTS idx_spatem_intersection ON spatem_messages(intersection_id);
CREATE INDEX IF NOT EXISTS idx_spatem_packet_id ON spatem_messages(packet_id);

-- SREM indexes
CREATE INDEX IF NOT EXISTS idx_srem_generation_time ON srem_messages(generation_time);
CREATE INDEX IF NOT EXISTS idx_srem_intersection ON srem_messages(intersection_id);
CREATE INDEX IF NOT EXISTS idx_srem_packet_id ON srem_messages(packet_id);

-- SSEM indexes
CREATE INDEX IF NOT EXISTS idx_ssem_generation_time ON ssem_messages(generation_time);
CREATE INDEX IF NOT EXISTS idx_ssem_intersection ON ssem_messages(intersection_id);
CREATE INDEX IF NOT EXISTS idx_ssem_packet_id ON ssem_messages(packet_id);

-- NEW: Correlation fields for OBU-RSU linkage and map visualization

-- Add correlation columns to MAPEM table
ALTER TABLE mapem_messages ADD COLUMN IF NOT EXISTS publisher_id VARCHAR(50);

-- Add correlation columns to SPATEM table
ALTER TABLE spatem_messages ADD COLUMN IF NOT EXISTS publisher_id VARCHAR(50);

-- Add correlation columns to SREM table
ALTER TABLE srem_messages ADD COLUMN IF NOT EXISTS intersection_id INTEGER;
ALTER TABLE srem_messages ADD COLUMN IF NOT EXISTS request_id VARCHAR(50);
ALTER TABLE srem_messages ADD COLUMN IF NOT EXISTS requestor_id VARCHAR(50);
ALTER TABLE srem_messages ADD COLUMN IF NOT EXISTS required_accuracy VARCHAR(50);

-- Create junction table for OBU-RSU correlations (links SREM requests to SSEM responses)
CREATE TABLE IF NOT EXISTS obu_rsu_correlations (
    id SERIAL PRIMARY KEY,
    srem_id INTEGER REFERENCES srem_messages(id) ON DELETE SET NULL,
    ssem_id INTEGER REFERENCES ssem_messages(id) ON DELETE SET NULL,
    mapem_id INTEGER REFERENCES mapem_messages(id) ON DELETE SET NULL,
    spatem_id INTEGER REFERENCES spatem_messages(id) ON DELETE SET NULL,
    obu_station_id VARCHAR(50),
    rsu_intersection_id VARCHAR(50),
    request_id VARCHAR(50),
    correlation_type VARCHAR(20), -- 'strict' (requestId match) or 'fallback' (time+intersection match)
    match_confidence NUMERIC(3,2) DEFAULT 1.0, -- 1.0 = strict, <1.0 = fallback
    srem_timestamp TIMESTAMP,
    ssem_timestamp TIMESTAMP,
    time_delta_ms INTEGER, -- milliseconds between SREM and SSEM
    request_type VARCHAR(50),
    status_code VARCHAR(50),
    granted_duration INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for correlation queries and performance
-- MAPEM correlation index
CREATE INDEX IF NOT EXISTS idx_mapem_publisher_id ON mapem_messages(publisher_id);
CREATE INDEX IF NOT EXISTS idx_mapem_generation_station ON mapem_messages(generation_time, station_id);

-- SPATEM correlation index
CREATE INDEX IF NOT EXISTS idx_spatem_publisher_id ON spatem_messages(publisher_id);
CREATE INDEX IF NOT EXISTS idx_spatem_generation_station ON spatem_messages(generation_time, station_id);

-- SREM correlation indexes
CREATE INDEX IF NOT EXISTS idx_srem_request_id ON srem_messages(request_id);
CREATE INDEX IF NOT EXISTS idx_srem_requestor_id ON srem_messages(requestor_id);
CREATE INDEX IF NOT EXISTS idx_srem_generation_intersection ON srem_messages(generation_time, intersection_id);
CREATE INDEX IF NOT EXISTS idx_srem_request_id_generation_time ON srem_messages(request_id, generation_time);
CREATE INDEX IF NOT EXISTS idx_srem_intersection_id_generation_time ON srem_messages(intersection_id, generation_time);
CREATE INDEX IF NOT EXISTS idx_srem_station_id_generation_time ON srem_messages(station_id, generation_time);
CREATE INDEX IF NOT EXISTS idx_srem_intersection_name_lower ON srem_messages(LOWER(TRIM(intersection_name)));

-- SSEM correlation indexes
CREATE INDEX IF NOT EXISTS idx_ssem_request_id_ref ON ssem_messages(request_id_ref);
CREATE INDEX IF NOT EXISTS idx_ssem_responder_id ON ssem_messages(responder_id);
CREATE INDEX IF NOT EXISTS idx_ssem_generation_intersection ON ssem_messages(generation_time, intersection_id);
CREATE INDEX IF NOT EXISTS idx_ssem_request_id_ref_generation_time ON ssem_messages(request_id_ref, generation_time);
CREATE INDEX IF NOT EXISTS idx_ssem_intersection_id_generation_time ON ssem_messages(intersection_id, generation_time);
CREATE INDEX IF NOT EXISTS idx_ssem_request_station_id_ref_generation_time ON ssem_messages(request_station_id_ref, generation_time);
CREATE INDEX IF NOT EXISTS idx_ssem_intersection_name_lower ON ssem_messages(LOWER(TRIM(intersection_name)));

-- Correlation table indexes
CREATE INDEX IF NOT EXISTS idx_correlations_request_id ON obu_rsu_correlations(request_id);
CREATE INDEX IF NOT EXISTS idx_correlations_srem_id ON obu_rsu_correlations(srem_id);
CREATE INDEX IF NOT EXISTS idx_correlations_ssem_id ON obu_rsu_correlations(ssem_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_correlations_unique_srem_ssem ON obu_rsu_correlations(srem_id, ssem_id)
    WHERE srem_id IS NOT NULL AND ssem_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_correlations_obu_rsu ON obu_rsu_correlations(obu_station_id, rsu_intersection_id);
CREATE INDEX IF NOT EXISTS idx_correlations_timestamp ON obu_rsu_correlations(srem_timestamp, ssem_timestamp);
CREATE INDEX IF NOT EXISTS idx_correlations_type ON obu_rsu_correlations(correlation_type);

-- Processed files table for idempotent cumulative ingestion
CREATE TABLE IF NOT EXISTS processed_files (
    id SERIAL PRIMARY KEY,
    file_name VARCHAR(255) NOT NULL,
    file_size BIGINT NOT NULL,
    file_hash CHAR(64) NOT NULL,
    packet_count INTEGER NOT NULL DEFAULT 0,
    status VARCHAR(20) NOT NULL DEFAULT 'Processed',
    processed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE processed_files ADD COLUMN IF NOT EXISTS packet_count INTEGER NOT NULL DEFAULT 0;
ALTER TABLE processed_files ADD COLUMN IF NOT EXISTS status VARCHAR(20) NOT NULL DEFAULT 'Processed';
ALTER TABLE processed_files ADD COLUMN IF NOT EXISTS processed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP;

CREATE UNIQUE INDEX IF NOT EXISTS idx_processed_files_hash_unique ON processed_files(file_hash);
CREATE INDEX IF NOT EXISTS idx_processed_files_name ON processed_files(file_name);
CREATE INDEX IF NOT EXISTS idx_processed_files_processed_at ON processed_files(processed_at DESC);

-- Station profile projection tables
CREATE TABLE IF NOT EXISTS station_profiles (
    station_id VARCHAR(50) PRIMARY KEY,
    entity_type VARCHAR(20) NOT NULL,
    station_type INTEGER,
    vehicle_category VARCHAR(100),
    supports_secure_comm BOOLEAN NOT NULL DEFAULT FALSE,
    supports_signed BOOLEAN NOT NULL DEFAULT FALSE,
    supports_encrypted BOOLEAN NOT NULL DEFAULT FALSE,
    first_seen_at TIMESTAMP NOT NULL,
    last_seen_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE station_profiles ADD COLUMN IF NOT EXISTS entity_type VARCHAR(20) NOT NULL DEFAULT 'Unknown';
ALTER TABLE station_profiles ADD COLUMN IF NOT EXISTS station_type INTEGER;
ALTER TABLE station_profiles ADD COLUMN IF NOT EXISTS vehicle_category VARCHAR(100);
ALTER TABLE station_profiles ADD COLUMN IF NOT EXISTS supports_secure_comm BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE station_profiles ADD COLUMN IF NOT EXISTS supports_signed BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE station_profiles ADD COLUMN IF NOT EXISTS supports_encrypted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE station_profiles ADD COLUMN IF NOT EXISTS first_seen_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE station_profiles ADD COLUMN IF NOT EXISTS last_seen_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE station_profiles ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP;

CREATE TABLE IF NOT EXISTS station_profile_message_types (
    station_id VARCHAR(50) NOT NULL REFERENCES station_profiles(station_id) ON DELETE CASCADE,
    message_type VARCHAR(20) NOT NULL,
    first_seen_at TIMESTAMP NOT NULL,
    last_seen_at TIMESTAMP NOT NULL,
    PRIMARY KEY (station_id, message_type)
);

CREATE INDEX IF NOT EXISTS idx_station_profiles_entity_type ON station_profiles(entity_type);
CREATE INDEX IF NOT EXISTS idx_station_profiles_station_type ON station_profiles(station_type);
CREATE INDEX IF NOT EXISTS idx_station_profiles_vehicle_category ON station_profiles(vehicle_category);
CREATE INDEX IF NOT EXISTS idx_station_profiles_supports_secure_comm ON station_profiles(supports_secure_comm);
CREATE INDEX IF NOT EXISTS idx_station_profiles_last_seen_at ON station_profiles(last_seen_at DESC);
CREATE INDEX IF NOT EXISTS idx_station_profile_message_types_type ON station_profile_message_types(message_type);
