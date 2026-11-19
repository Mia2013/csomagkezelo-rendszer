import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
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
import Check from "@mui/icons-material/Check";
import Close from "@mui/icons-material/Close";
import Done from "@mui/icons-material/Done";
import LocalShipping from "@mui/icons-material/LocalShipping";

import { endpoints, getData, putData } from "../../api/apiCalls";
import { useAuth } from "../../provider/AuthContext";
import { TASK_TYPES, WEEKDAYS } from "../../constants/constants";
import dayjs from "../../utils/dayjs";
import CourierWeekDays from "./CourierWeekDays";

const today = dayjs().format("YYYY-MM-DD");

const CourierTaskList = ({ onLoaded }) => {
  const { user, setAlert } = useAuth();
  const [tasks, setTasks] = useState([]);
  const [selectedDay, setSelectedDay] = useState(today);
  const [tab, setTab] = useState(0);

  const pickupTasks = tasks.filter((task) => task.taskType === "Pickup");
  const deliveryTasks = tasks.filter((task) => task.taskType === "Delivery");

  const loadTasks = async () => {
    try {
      const pickups = await getData(`${endpoints.pickuptasks}/${user.userId}`);
      const deliveries = await getData(`${endpoints.deliverytasks}/${user.userId}`);
      setTasks([...pickups, ...deliveries]);
      onLoaded?.(pickups.length + deliveries.length);
    } catch (error) {
      setAlert({ message: error.message, severity: "error" });
    }
  };

  useEffect(() => {
    loadTasks();
  }, [setAlert]);

  const getDay = (task) => dayjs(task.pickupDate).format("YYYY-MM-DD");

  let shownTasks = tasks;
  if (tab === 1) shownTasks = pickupTasks;
  if (tab === 2) shownTasks = deliveryTasks;

  const dayTasks = shownTasks
    .filter((task) => getDay(task) === selectedDay)
    .sort((a, b) => (a.pickupSlot ?? "").localeCompare(b.pickupSlot ?? ""));

  const assignedDeliveries = dayTasks.filter((task) => task.taskType === "Delivery" && task.status === "Assigned");

  const taskDays = shownTasks.map(getDay).sort();
  const prevDay = taskDays.filter((day) => day < selectedDay).pop();
  const nextDay = taskDays.find((day) => day > selectedDay);

  const updateTask = async (endpoint, task, message) => {
    try {
      await putData(`${endpoint}/${task.packageId}`, { courierId: user.userId });
      setAlert({ message, severity: "success" });
      loadTasks();
    } catch (error) {
      setAlert({ message: error.message, severity: "error" });
    }
  };

  const startAllDeliveries = async () => {
    try {
      for (const task of assignedDeliveries) {
        await putData(`${endpoints.pickupfromwarehouse}/${task.packageId}`, { courierId: user.userId });
      }
      setAlert({ message: "Elindultál a csomagokkal a raktárból.", severity: "success" });
    } catch (error) {
      setAlert({ message: error.message, severity: "error" });
    }
    loadTasks();
  };

  const failDelivery = async (task) => {
    try {
      await putData(`${endpoints.failDelivery}/${task.id}`);
      setAlert({ message: "Sikertelen kézbesítés rögzítve.", severity: "success" });
      loadTasks();
    } catch (error) {
      setAlert({ message: error.message, severity: "error" });
    }
  };

  return (
    <Paper sx={{ p: 4 }}>
      <Tabs value={tab} onChange={(e, value) => setTab(value)} sx={{ mb: 2 }}>
        <Tab label={`Összes (${tasks.length})`} value={0} />
        <Tab label={`Felvétel (${pickupTasks.length})`} value={1} />
        <Tab label={`Kiszállítás (${deliveryTasks.length})`} value={2} />
      </Tabs>

      <CourierWeekDays tasks={shownTasks} selectedDay={selectedDay} onSelect={setSelectedDay} />

      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <IconButton disabled={!prevDay} onClick={() => setSelectedDay(prevDay)} aria-label="Előző munkanap">
          <ChevronLeft />
        </IconButton>
        <Typography variant="h5">
          {dayjs(selectedDay).format("YYYY. MM. DD.")} {WEEKDAYS[dayjs(selectedDay).day()].toUpperCase()}
          {selectedDay === today && " (MA)"}
        </Typography>
        <IconButton disabled={!nextDay} onClick={() => setSelectedDay(nextDay)} aria-label="Következő munkanap">
          <ChevronRight />
        </IconButton>
      </Box>

      {assignedDeliveries.length > 0 && (
        <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 2 }}>
          <Button variant="contained" color="warning" startIcon={<LocalShipping />} onClick={startAllDeliveries}>
            Mind úton ({assignedDeliveries.length})
          </Button>
        </Box>
      )}

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
              {dayTasks.map((task) => (
                <TableRow key={task.id}>
                  <TableCell>
                    <Chip
                      size="small"
                      variant="outlined"
                      label={TASK_TYPES[task.taskType].label}
                      color={TASK_TYPES[task.taskType].color}
                    />
                  </TableCell>
                  <TableCell>{task.trackingNumber}</TableCell>
                  <TableCell>{task.taskType === "Pickup" ? task.senderAddress : task.recipientAddress}</TableCell>
                  <TableCell>{task.size}</TableCell>
                  <TableCell>{task.pickupSlot}</TableCell>
                  <TableCell>
                    <Box sx={{ display: "flex", gap: 1 }}>
                      {task.taskType === "Pickup" && (
                        <Button
                          variant="contained"
                          size="small"
                          color="info"
                          startIcon={<Check />}
                          onClick={() => updateTask(endpoints.pickuppackage, task, "A csomagot felvetted.")}
                        >
                          Felvéve
                        </Button>
                      )}
                      {task.taskType === "Delivery" && task.status === "Assigned" && (
                        <Button
                          variant="contained"
                          size="small"
                          color="warning"
                          startIcon={<LocalShipping />}
                          onClick={() =>
                            updateTask(endpoints.pickupfromwarehouse, task, "Elindultál a csomaggal a raktárból.")
                          }
                        >
                          Úton
                        </Button>
                      )}
                      {task.taskType === "Delivery" && task.status === "InProgress" && (
                        <>
                          <Button
                            variant="contained"
                            size="small"
                            color="success"
                            startIcon={<Done />}
                            onClick={() => updateTask(endpoints.deliver, task, "A csomagot kézbesítetted.")}
                          >
                            Kézbesítve
                          </Button>
                          <Button
                            variant="outlined"
                            size="small"
                            color="error"
                            startIcon={<Close />}
                            onClick={() => failDelivery(task)}
                          >
                            Sikertelen
                          </Button>
                        </>
                      )}
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  );
};

export default CourierTaskList;
