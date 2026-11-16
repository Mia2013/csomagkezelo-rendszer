import { useEffect, useState } from "react";
import {
  Alert,
  Chip,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import ChevronLeft from "@mui/icons-material/ChevronLeft";
import ChevronRight from "@mui/icons-material/ChevronRight";

import { endpoints, getData } from "../api/apiCalls";
import { useAuth } from "../provider/AuthContext";
import dayjs from "../utils/dayjs";

const PERIODS = [
  { value: "weekly", label: "Heti" },
  { value: "monthly", label: "Havi" },
];

const MONTHS = [
  "január", "február", "március", "április", "május", "június",
  "július", "augusztus", "szeptember", "október", "november", "december",
];

const startOfPeriod = (date, period) => {
  if (period === "weekly") {
    return dayjs(date).subtract((dayjs(date).day() + 6) % 7, "day").startOf("day");
  }
  return dayjs(date).startOf("month");
};

const stepPeriod = (start, period, step) =>
  period === "weekly" ? start.add(7 * step, "day") : start.add(step, "month");

const periodLabel = (start, period) => {
  if (period === "weekly") {
    return `${start.format("YYYY. MM. DD.")} – ${start.add(6, "day").format("MM. DD.")}`;
  }
  return `${start.year()}. ${MONTHS[start.month()]}`;
};

const CourierHistory = () => {
  const { user, setAlert } = useAuth();
  const [period, setPeriod] = useState("weekly");
  const [current, setCurrent] = useState(() => startOfPeriod(dayjs(), "weekly"));
  const [tasks, setTasks] = useState([]);

  useEffect(() => {
    getData(`${endpoints.history}/${user.userId}`, { intervall: "all" })
      .then((data) => setTasks(data.tasks))
      .catch((error) => setAlert({ message: error.message, severity: "error" }));
  }, [user.userId, setAlert]);

  const changePeriod = (value) => {
    setPeriod(value);
    setCurrent(startOfPeriod(dayjs(), value));
  };

  const next = stepPeriod(current, period, 1);
  const isLatest = !current.isBefore(startOfPeriod(dayjs(), period));

  const periodTasks = tasks.filter((task) => {
    const time = dayjs(task.assignmentTime);
    return !time.isBefore(current) && time.isBefore(next);
  });
  const activeDays = new Set(periodTasks.map((task) => dayjs(task.assignmentTime).format("YYYY-MM-DD"))).size;

  return (
    <Paper sx={{ p: 4 }}>
      <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <ToggleButtonGroup
          exclusive
          size="small"
          value={period}
          onChange={(e, value) => value && changePeriod(value)}
        >
          {PERIODS.map((item) => (
            <ToggleButton key={item.value} value={item.value}>
              {item.label}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>

        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <IconButton onClick={() => setCurrent(stepPeriod(current, period, -1))} aria-label="Előző">
            <ChevronLeft />
          </IconButton>
          <Typography variant="h6">{periodLabel(current, period)}</Typography>
          <IconButton disabled={isLatest} onClick={() => setCurrent(next)} aria-label="Következő">
            <ChevronRight />
          </IconButton>
        </Stack>
      </Stack>

      <Stack direction="row" spacing={1} sx={{ mb: 3 }}>
        <Chip label={`Lezárt feladat: ${periodTasks.length}`} color="primary" />
        <Chip label={`Aktív nap: ${activeDays}`} variant="outlined" />
      </Stack>

      {periodTasks.length === 0 ? (
        <Alert severity="info">Ebben az időszakban nincs lezárt feladatod.</Alert>
      ) : (
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow sx={{ bgcolor: "#e3f2fd" }}>
                <TableCell>Követési szám</TableCell>
                <TableCell>Dátum</TableCell>
                <TableCell>Feladó címe</TableCell>
                <TableCell>Címzett címe</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {periodTasks.map((task) => (
                <TableRow key={task.taskId}>
                  <TableCell>{task.trackingNumber}</TableCell>
                  <TableCell>{dayjs(task.assignmentTime).format("YYYY. MM. DD. HH:mm")}</TableCell>
                  <TableCell>{task.senderAddress}</TableCell>
                  <TableCell>{task.recipientAddress}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  );
};

export default CourierHistory;
