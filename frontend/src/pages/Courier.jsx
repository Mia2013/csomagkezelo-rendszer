import { useState } from "react";
import { Box } from "@mui/material";
import History from "@mui/icons-material/History";
import LocalShipping from "@mui/icons-material/LocalShipping";

import Dashboard from "../components/Dashboard";
import CourierTaskList from "../components/Courier/CourierTaskList";
import CourierHistory from "../components/Courier/CourierHistory";

const CourierPage = () => {
  const [selectedId, setSelectedId] = useState("tasks");
  const [counts, setCounts] = useState({});
  const [reloadKey, setReloadKey] = useState(0);

  const setCount = (id) => (count) => setCounts((prev) => ({ ...prev, [id]: count }));

  const selectCard = (id) => {
    setSelectedId(id);
    setReloadKey((prev) => prev + 1);
  };

  const options = [
    {
      id: "tasks",
      title: "Feladatok",
      icon: <LocalShipping />,
      count: counts.tasks ?? 0,
      render: () => <CourierTaskList onLoaded={setCount("tasks")} />,
    },
    {
      id: "history",
      title: "Előzmények",
      icon: <History />,
      count: counts.history ?? 0,
      render: () => <CourierHistory onLoaded={setCount("history")} />,
    },
  ];

  return (
    <Dashboard
      title="Futár"
      icon={<LocalShipping />}
      cards={options}
      selectedId={selectedId}
      onSelect={selectCard}
    >
      {options.map((option) => (
        <Box key={`${option.id}-${reloadKey}`} sx={{ display: option.id === selectedId ? "block" : "none" }}>
          {option.render()}
        </Box>
      ))}
    </Dashboard>
  );
};

export default CourierPage;
