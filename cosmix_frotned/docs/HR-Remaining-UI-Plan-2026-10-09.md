# Remaining HR Web UI Implementation

Continue the approved SRS web delivery in the existing React project. This increment
replaces generic-only records layouts with date-oriented attendance/leave calendars,
weekly roster and monthly-register surfaces, and an editable zone-boundary preview.
Existing hiring, authentication, permissions and API integrations remain unchanged.

1. Add deterministic Pakistan business-date/calendar helpers and verify month boundaries,
   leap years, Monday-first weeks and invalid inputs without persisting test data.
2. Add reusable date navigation, accessible calendars, weekly roster and monthly day
   matrix. Wire the appropriate SRS attendance, leave, roster, report and own views.
   Unknown records must remain unavailable, never absent, zero or empty approved history.
3. Add circle/Polygon preview and vertex editing using local coordinates. No external
   map service, employee locations, browser geolocation or provider credentials are used.
   Geographic basemap remains unavailable pending approved provider configuration.
4. Improve remaining workspace navigation and report/export controls without inventing
   backend DTOs/endpoints or enabling unsupported business actions.
5. Run focused helper checks, npm run verify and actual authenticated browser checks at
   desktop/mobile sizes. Document passed and blocked checks and remaining integration work.

Calendar and geometry view models describe frontend presentation only. They are not
final API contracts. Drafts remain in memory, dirty navigation stays protected, and
switching account/tab must discard private transient values. No backend/mobile edits,
database writes, scanner/SMS changes, commits or pushes.
