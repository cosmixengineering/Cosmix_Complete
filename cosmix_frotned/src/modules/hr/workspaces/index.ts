import { attendanceWorkspaces } from './attendance';
import { operationsWorkspaces } from './operations';
import { peopleServiceWorkspaces } from './people-services';
import { reportingWorkspaces } from './reporting';

export const hrPreparedWorkspaces = [
  ...attendanceWorkspaces,
  ...operationsWorkspaces,
  ...peopleServiceWorkspaces,
  ...reportingWorkspaces,
];
