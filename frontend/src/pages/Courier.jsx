import { useEffect, useState } from "react";
import History from "@mui/icons-material/History";
import LocalShipping from "@mui/icons-material/LocalShipping";

import { endpoints, getData } from "../api/apiCalls";
import { useAuth } from "../provider/AuthContext";
import Dashboard from "../components/Dashboard";
import CourierTaskList from "../components/CourierTaskList";
import CourierHistory from "../components/CourierHistory";

const CourierPage = () => {
  const { user, setAlert } = useAuth();
  const [tasks, setTasks] = useState([]);
  const [weeklyCount, setWeeklyCount] = useState(0);
  const [selectedId, setSelectedId] = useState("tasks");

  const loadTasks = () =>
    Promise.all([
      getData(`${endpoints.pickuptasks}/${user.userId}`),
      getData(`${endpoints.deliverytasks}/${user.userId}`),
    ])
      .then(([pickups, deliveries]) => setTasks([...pickups, ...deliveries]))
      .catch((error) => setAlert({ message: error.message, severity: "error" }));

  const loadWeeklyCount = () =>
    getData(`${endpoints.history}/${user.userId}`, { intervall: "weekly" })
      .then((data) => setWeeklyCount(data.totalCompletedTasks))
      .catch((error) => setAlert({ message: error.message, severity: "error" }));

  const reload = () => {
    loadTasks();
    loadWeeklyCount();
  };

  useEffect(() => {
    reload();
  }, [setAlert]);

  const options = [
    {
      id: "tasks",
      title: "Feladatok",
      icon: <LocalShipping />,
      count: tasks.length,
      render: () => <CourierTaskList tasks={tasks} onChanged={reload} />,
    },
    {
      id: "history",
      title: "Előzmények",
      icon: <History />,
      count: weeklyCount,
      render: () => <CourierHistory />,
    },
  ];

  const selected = options.find((option) => option.id === selectedId);

  return (
    <Dashboard
      title="Futár"
      icon={<LocalShipping />}
      cards={options}
      selectedId={selectedId}
      onSelect={setSelectedId}
    >
      {selected.render()}
    </Dashboard>
  );
};

export default CourierPage;
