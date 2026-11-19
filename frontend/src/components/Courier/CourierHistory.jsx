import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Chip,
  IconButton,
  Paper,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  Typography,
} from "@mui/material";
import ChevronLeft from "@mui/icons-material/ChevronLeft";
import ChevronRight from "@mui/icons-material/ChevronRight";

import { endpoints, getData } from "../../api/apiCalls";
import { useAuth } from "../../provider/AuthContext";
import { MONTHS, TASK_RESULTS, TASK_TYPES, WEEKDAYS } from "../../constants/constants";
import dayjs from "../../utils/dayjs";

const getPeriodStart = (date, period) => {
  if (period === "daily") {
    return dayjs(date).startOf("day");
  }
  if (period === "weekly") {
    return dayjs(date).subtract((dayjs(date).day() + 6) % 7, "day").startOf("day");
  }
  return dayjs(date).startOf("month");
};

const getNextPeriodStart = (start, period, step) => {
  if (period === "daily") {
    return start.add(step, "day");
  }
  if (period === "weekly") {
    return start.add(7 * step, "day");
  }
  return start.add(step, "month");
};

const getPeriodLabel = (start, period) => {
  if (period === "daily") {
    return `${start.format("YYYY. MM. DD.")} ${WEEKDAYS[start.day()]}`;
  }
  if (period === "weekly") {
    return `${start.format("YYYY. MM. DD.")} – ${start.add(6, "day").format("MM. DD.")}`;
  }
  return `${start.year()}. ${MONTHS[start.month()]}`;
};

const CourierHistory = ({ onLoaded }) => {
  const { user, setAlert } = useAuth();
  const [tasks, setTasks] = useState([]);
  const [period, setPeriod] = useState("weekly");
  const [periodStart, setPeriodStart] = useState(getPeriodStart(dayjs(), "weekly"));

  const loadHistory = async () => {
    try {
      const data = await getData(`${endpoints.history}/${user.userId}`, { intervall: "all" });
      setTasks(data.tasks);
      const weekStart = getPeriodStart(dayjs(), "weekly");
      onLoaded?.(data.tasks.filter((task) => !dayjs(task.assignmentTime).isBefore(weekStart)).length);
    } catch (error) {
      setAlert({ message: error.message, severity: "error" });
    }
  };

  useEffect(() => {
    loadHistory();
  }, [setAlert]);

  const changePeriod = (value) => {
    setPeriod(value);
    setPeriodStart(getPeriodStart(dayjs(), value));
  };

  const nextPeriodStart = getNextPeriodStart(periodStart, period, 1);
  const isCurrentPeriod = !periodStart.isBefore(getPeriodStart(dayjs(), period));

  const periodTasks = tasks.filter((task) => {
    const time = dayjs(task.assignmentTime);
    return !time.isBefore(periodStart) && time.isBefore(nextPeriodStart);
  });

  const workDays = periodTasks.map((task) => dayjs(task.assignmentTime).format("YYYY-MM-DD"));
  const activeDays = new Set(workDays).size;
  const failedCount = periodTasks.filter((task) => task.taskStatus === "Failed").length;

  const getTaskType = (task) => TASK_TYPES[task.taskType] ?? { label: "–", color: "default" };
  const getResult = (task) => TASK_RESULTS[task.taskType + task.taskStatus] ?? { label: "Ismeretlen", color: "default" };

  return (
    <Paper sx={{ p: 4 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Tabs value={period} onChange={(e, value) => changePeriod(value)}>
          <Tab label="Napi" value="daily" />
          <Tab label="Heti" value="weekly" />
          <Tab label="Havi" value="monthly" />
        </Tabs>

        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <IconButton
            onClick={() => setPeriodStart(getNextPeriodStart(periodStart, period, -1))}
            aria-label="Előző"
          >
            <ChevronLeft />
          </IconButton>
          <Typography variant="h6">{getPeriodLabel(periodStart, period)}</Typography>
          <IconButton disabled={isCurrentPeriod} onClick={() => setPeriodStart(nextPeriodStart)} aria-label="Következő">
            <ChevronRight />
          </IconButton>
        </Box>
      </Box>

      <Box sx={{ display: "flex", gap: 1, mb: 3 }}>
        <Chip label={`Lezárt feladat: ${periodTasks.length}`} color="primary" />
        {failedCount > 0 && <Chip label={`Sikertelen: ${failedCount}`} color="error" variant="outlined" />}
        {period !== "daily" && <Chip label={`Aktív nap: ${activeDays}`} variant="outlined" />}
      </Box>

      {periodTasks.length === 0 ? (
        <Alert severity="info">Ebben az időszakban nincs lezárt feladatod.</Alert>
      ) : (
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow sx={{ bgcolor: "#e3f2fd" }}>
                <TableCell>Feladat</TableCell>
                <TableCell>Eredmény</TableCell>
                <TableCell>Követési szám</TableCell>
                <TableCell>Dátum</TableCell>
                <TableCell>Feladó címe</TableCell>
                <TableCell>Címzett címe</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {periodTasks.map((task) => (
                <TableRow key={task.taskId}>
                  <TableCell>
                    <Chip
                      size="small"
                      variant="outlined"
                      label={getTaskType(task).label}
                      color={getTaskType(task).color}
                    />
                  </TableCell>
                  <TableCell>
                    <Chip size="small" label={getResult(task).label} color={getResult(task).color} />
                  </TableCell>
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
