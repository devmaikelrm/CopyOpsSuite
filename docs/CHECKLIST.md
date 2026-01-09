# Checklist de avances

## A. UX / UI Copy + Mini windows
- [ ] A1) Salud en grid (chips, tintes) + fila comprimida y advertencias USB2.
- [ ] A2) Barra global de progreso y tooltips honesto por destino.
- [ ] A3) Chips de estado (READING/WRITING/BUFFER WAIT/PAUSED/DONE) y máquina de estados en el engine.
- [ ] A4) Contadores compactos (FS/BusHint/Workers/Queue) y señal de warning para USB2 saturado.

## B. Modo operador/admin
- [ ] B1) Ajuste en Settings: toggle Operador, PIN con SHA256, bloqueos de edición, botón bloquear Admin, rol guardado en settings.

## C. Pre-checks y recalculo de precio
- [ ] C1) Rutina de pre-check antes de iniciar (validaciones por destino, persistencia, eventos), modal/infoBar, Force Start admin.
- [ ] C2) Recalculado de esperado ahora/100% cada segundo, tanto en resumen Copy como Checkout.

## D. Robustez y facturación
- [ ] D1) Continuar tras errores, ajustes de billing mode (Por target / Por job), reflejar en docs/UI.
- [ ] D2) Alerta cuando un target falla y recalcula precio esperado en vivo.

## E. Caja y redondeo transparente
- [ ] E1) Mostrar GB real/billable y documentar en notas/campo adicional.
- [ ] E2) Agrupar totales por moneda en CashView.

## F. Auditoría y snapshots
- [ ] F1) Timeline del job en JobDetailWindow con eventos relevantes.
- [ ] F2) Registro de snapshot post-job y panel de snapshots en HistoryView.
- [ ] F3) Eventos auditivos enriquecidos (BUFFER_ENABLED, TARGET_SLOW_USB2, etc.).

## G. Diagnóstico y simulación
- [ ] G1) Botones “Run Diagnostics” (Settings y Audit Reports) y pruebas buffer con auditoría y InfoBars.
- [ ] G2) Botón “Simular” en Copy, estimaciones por destino, diálogo y evento.

## H. Docs y cierre
- [ ] H1) Especificar todo en docs/SPEC.md (admin, prechecks, billing, simulación, snapshots).
- [ ] H2) Validar build final sin advertencias, UI fluido y logs acotados.

## Stage 3 UI Plan
- [x] A) Shared DeviceRowViewModel + HealthBrushConverter + throttled CopyViewModel updates (DeviceRows, logs limit, tooltip).
- [ ] B) CopyView layout revised (three columns, health/state chips, progress tooltip, precheck dialog) and Start/precheck logic.
- [ ] C) MiniTransferWindow wiring (ViewModel consumption, auto-open, state/queue display, buttons).
- [ ] D) JobDetail window: List, Validations, Errors, In RAM stats, Timeline + openers (mini/details/history/audit).
- [ ] E) SettingsView: Operator/Admin toggle, buffering controls, diagnostics button with InfoBar + PIN handling.
- [ ] F) CashView: Quick Checkout expected now/100%, rounding transparency, today's cards grouped by currency; Sales filters.
- [ ] G) AuditView: Events filters, Alerts with resolvers, Simulation results view, alert persist.
- [ ] H) Docs/spec update + final build/check (handle MSB3073 guidance in doc).
