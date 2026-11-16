import { useState } from "react";
import {
  Alert,
  Button,
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
import Check from "@mui/icons-material/Check";
import Close from "@mui/icons-material/Close";
import Done from "@mui/icons-material/Done";
import LocalShipping from "@mui/icons-material/LocalShipping";

import { endpoints, putData } from "../api/apiCalls";
import { useAuth } from "../provider/AuthContext";
import dayjs from "../utils/dayjs";

import {TASK_TYPES, WEEKDAYS} from "../constants/constants"

const getAction = (task) => {
  if (task.taskType === "Pickup") {
    return {
      endpoint: endpoints.pickuppackage,
      btnLabel: "Felvéve",
      icon: <Check />,
      color: "info",
      failBtnLabel: true,
      message: "A csomagot felvetted.",
    };
  }
  if (task.status === "InProgress") {
    return {
      endpoint: endpoints.deliver,
      btnLabel: "Kézbesítve",
      icon: <Done />,
      color: "success",
      failBtnLabel: true,
      message: "A csomagot kézbesítetted.",
    };
  }
  return {
    endpoint: endpoints.pickupfromwarehouse,
    btnLabel: "Úton",
    icon: <LocalShipping />,
    color: "warning",
    message: "Elindultál a csomaggal a raktárból.",
  };
};

const getDay = (task) => dayjs(task.pickupDate).format("YYYY-MM-DD");

const formatDay = (day) => `${dayjs(day).format("YYYY. MM. DD.")} ${WEEKDAYS[dayjs(day).day()]}`;

const CourierTaskList = ({ tasks, onChanged }) => {
  const { user, setAlert } = useAuth();
  const [today] = useState(() => dayjs().format("YYYY-MM-DD"));
  const [day, setDay] = useState(today);
  const [typeFilter, setTypeFilter] = useState("all");

  const filteredTasks = typeFilter === "all" ? tasks : tasks.filter((task) => task.taskType === typeFilter);
  const workDays = [...new Set(filteredTasks.map(getDay))].sort();
  const prevDay = [...workDays].reverse().find((d) => d < day);
  const nextDay = workDays.find((d) => d > day);
  const dayTasks = filteredTasks
    .filter((task) => getDay(task) === day)
    .sort((a, b) => {
      if (!a.pickupSlot && !b.pickupSlot) return 0;
      if (!a.pickupSlot) return 1;
      if (!b.pickupSlot) return -1;
      return a.pickupSlot.localeCompare(b.pickupSlot);
    });

  const runAction = (task) => {
    const action = getAction(task);
    putData(`${action.endpoint}/${task.packageId}`, { courierId: user.userId })
      .then(() => {
        setAlert({ message: action.message, severity: "success" });
        onChanged();
      })
      .catch((error) => setAlert({ message: error.message, severity: "error" }));
  };

  return (
    <Paper sx={{ p: 4 }}>
      <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <IconButton disabled={!prevDay} onClick={() => setDay(prevDay)} aria-label="Előző munkanap">
          <ChevronLeft />
        </IconButton>
        <Typography variant="h5">
          {formatDay(day).toUpperCase()}
          {day === today && " (MA)"}
        </Typography>
        <IconButton disabled={!nextDay} onClick={() => setDay(nextDay)} aria-label="Következő munkanap">
          <ChevronRight />
        </IconButton>
      </Stack>

      <ToggleButtonGroup
        exclusive
        size="small"
        value={typeFilter}
        onChange={(_, value) => value && setTypeFilter(value)}
        sx={{ mb: 2 }}
      >
        <ToggleButton value="all">Összes</ToggleButton>
        {Object.entries(TASK_TYPES).map(([type, { label }]) => (
          <ToggleButton key={type} value={type}>
            {label}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>

      {dayTasks.length === 0 ? (
        <Alert severity="info">Ezen a napon nincs feladatod.</Alert>
      ) : (
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow sx={{ bgcolor: "#e3f2fd" }}>
                <TableCell>Feladat</TableCell>
                <TableCell>Követési szám</TableCell>
                <TableCell>Cím</TableCell>
                <TableCell>Méret</TableCell>
                <TableCell>Idősáv</TableCell>
                <TableCell>Művelet</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {dayTasks.map((task) => {
                const action = getAction(task);
                return (
                  <TableRow key={task.id}>
                    <TableCell>
                      <Chip
                        size="small"
                        label={TASK_TYPES[task.taskType].label}
                        color={TASK_TYPES[task.taskType].color}
                        variant="outlined"
                      />
                    </TableCell>
                    <TableCell>{task.trackingNumber}</TableCell>
                    <TableCell>{task.taskType === "Pickup" ? task.senderAddress : task.recipientAddress}</TableCell>
                    <TableCell>{task.size}</TableCell>
                    <TableCell>{task.pickupSlot}</TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={1}>
                        <Button
                          variant="contained"
                          size="small"
                          color={action.color}
                          startIcon={action.icon}
                          onClick={() => runAction(task)}
                        >
                          {action.btnLabel}
                        </Button>
                        {action.failBtnLabel && (
                          <Button variant="outlined" size="small" color="error" startIcon={<Close />} disabled>
                            Sikertelen
                          </Button>
                        )}
                      </Stack>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  );
};

export default CourierTaskList;
