import { Typography } from '@mui/material'

const ValidationCaption = ({ message }) => {
    return (
        <Typography variant='caption' sx={{ color: "#D3302F", ml: 2, mt: 0.5 }}>{message}</Typography>)
}

export default ValidationCaption