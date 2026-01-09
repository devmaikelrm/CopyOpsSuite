# Roadmap y Checklist

## Fase 1 – Modelos y Persistencia
- [x] Definir modelos Core y enums según spec.
- [x] Implementar SqliteDb con DDL y cargar datos semilla.
- [x] Repositorios async para settings, perfiles, jobs, targets, logs, ventas, eventos.
- [x] Documentar esquema y datos semilla en `docs/SPEC.md`.

## Fase 2 - Servicios del sistema y UI base
- [x] Crear SettingsService, DriveWatcher y RamMonitor en Core.
- [x] Mostrar dispositivos y RAM en CopyView con DataGrid y log limitados.
- [x] Conectar servicios/singletons en App startup y tirar eventos básicos.

## Fase 3 – Motores y UI avanzada
- [ ] Implementar MultiCopyEngine (planner/worker), eventos y mini ventanas.
- [ ] Construir CashOps (Billing, Caja) e integrarlo con CopyView/CashView.
- [ ] Mejorar AuditOps (eventos, alertas, sesiones) y JobDetailWindow.
- [ ] Añadir exportadores CSV y editor de perfiles.
- [ ] Realizar pulido visual (filtros, estilos, logs, mini-ventanas).
