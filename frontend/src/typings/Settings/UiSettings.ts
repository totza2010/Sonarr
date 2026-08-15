export default interface UiSettings {
  theme: 'auto' | 'dark' | 'light';
  showRelativeDates: boolean;
  shortDateFormat: string;
  longDateFormat: string;
  timeFormat: string;
  firstDayOfWeek: number;
  interactiveImportInlineActions: boolean;
  enableColorImpairedMode: boolean;
  calendarWeekColumnHeader: string;
}
