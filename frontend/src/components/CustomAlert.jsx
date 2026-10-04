import { Alert, Snackbar } from '@mui/material';

const CustomAlert = ({ alert, setAlert }) => {
    const handleClose = () => setAlert(null);

    return (
        <Snackbar open={!!alert.message} autoHideDuration={5000} onClose={handleClose}>
            <Alert
                onClose={handleClose}
                severity={alert.severity}
                variant="filled"
            >
                {alert?.message}
            </Alert>
        </Snackbar>)
}

export default CustomAlert;