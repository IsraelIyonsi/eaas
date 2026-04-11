// ============================================================
// EaaS Dashboard - Domain Types
// ============================================================

export type DomainStatus =
  | 'Verified'
  | 'PendingVerification'
  | 'Failed'
  | 'Suspended';

export interface DnsRecord {
  id?: string;
  type: string;
  name: string;
  value: string;
  purpose: string;
  isVerified: boolean;
}

export interface Domain {
  id: string;
  domainName: string;
  status: DomainStatus;
  dnsRecords: DnsRecord[];
  verifiedAt?: string;
  lastCheckedAt?: string;
  createdAt: string;
}

export interface DomainDetail extends Domain {
  inbound_enabled: boolean;
  mx_verified: boolean;
  inbound_rule_count: number;
}
