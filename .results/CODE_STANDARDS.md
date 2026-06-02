# Code Standards (Mandatory)

## General Principles
- Clarity over cleverness
- Explicit over implicit
- Consistency over preference

---

## Architectural Rules
- Clean Architecture must be respected

---

## SOLID (Advisory but Expected)
- Single Responsibility preferred at class and method level
- Open/Closed encouraged via extension, not modification
- Interface Segregation over fat contracts

---

## Database Standards (Hard Rules)

### Code-First

## EF Core Configuration

✅ Required:
- One `IEntityTypeConfiguration<T>` per entity
- Explicit column mapping
- Explicit constraints and indexes

❌ Forbidden:
- Inline configuration in `OnModelCreating`
- Convention-only critical mappings
